using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Configuration;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Features.Dashboard;
using ReviewInsights.Api.Features.History;
using ReviewInsights.Api.Features.Products;
using ReviewInsights.Api.Features.Reports;
using ReviewInsights.Api.Features.Reviews;
using ReviewInsights.Api.Features.Uploads;
using ReviewInsights.Api.Features.Worker;
using ReviewInsights.Api.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddOpenApi();

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });

    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 52_428_800; // 50 MB
    });
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 52_428_800;
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.EnableDynamicJson();
    var npgsqlDataSource = dataSourceBuilder.Build();
    builder.Services.AddSingleton(npgsqlDataSource);

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(npgsqlDataSource));

    var minioSettings = new MinioSettings();
    builder.Configuration.GetSection("MinIO").Bind(minioSettings);
    builder.Services.AddSingleton(minioSettings);
    builder.Services.AddSingleton<IFileStorageService, MinioFileStorageService>();

    var rabbitSettings = new RabbitMqSettings();
    builder.Configuration.GetSection("RabbitMQ").Bind(rabbitSettings);
    builder.Services.AddSingleton(rabbitSettings);
    builder.Services.AddSingleton<RabbitMqService>();
    builder.Services.AddSingleton<IQueueService>(sp => sp.GetRequiredService<RabbitMqService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqService>());
    builder.Services.AddHostedService<WorkerResultsConsumer>();

    var reportLimits = new ReportLimits();
    builder.Configuration.GetSection("ReportLimits").Bind(reportLimits);
    builder.Services.AddSingleton(reportLimits);

    builder.Services.AddSingleton<CsvJsonReviewParser>();
    builder.Services.AddSingleton<PdfReportRenderer>();

    builder.Services.AddScoped<UploadsService>();
    builder.Services.AddScoped<ReviewsService>();
    builder.Services.AddScoped<ProductsService>();
    builder.Services.AddScoped<ReportsService>();
    builder.Services.AddScoped<DashboardService>();
    builder.Services.AddScoped<WorkerService>();
    builder.Services.AddScoped<HistoryService>();

    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString);

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
    });

    var app = builder.Build();

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

            var (statusCode, message) = exception switch
            {
                ApiException api => (api.StatusCode, api.Message),
                BadHttpRequestException bad => (StatusCodes.Status400BadRequest, bad.Message),
                ArgumentException arg => (StatusCodes.Status400BadRequest, arg.Message),
                KeyNotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
            };

            if (statusCode >= 500)
            {
                logger.LogError(exception,
                    "Unhandled exception on {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);
            }
            else if (statusCode >= 400)
            {
                logger.LogWarning(
                    "Client error {StatusCode} on {Method} {Path}: {ErrorMessage}",
                    statusCode,
                    context.Request.Method,
                    context.Request.Path,
                    message);
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ErrorResponse.From(message));
        });
    });

    app.UseStatusCodePages();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent",
                httpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown");
            diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
        };
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex is not null || httpContext.Response.StatusCode >= 500)
                return Serilog.Events.LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 400)
                return Serilog.Events.LogEventLevel.Warning;
            if (httpContext.Request.Path.StartsWithSegments("/health"))
                return Serilog.Events.LogEventLevel.Debug;
            return Serilog.Events.LogEventLevel.Information;
        };
    });

    app.MapOpenApi();

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
    }

    app.UseCors();

    app.MapHealthChecks("/health");

    app.MapDashboardEndpoints();
    app.MapReviewsEndpoints();
    app.MapProductsEndpoints();
    app.MapReportsEndpoints();
    app.MapUploadsEndpoints();
    app.MapHistoryEndpoints();

    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        logger.LogInformation("Applying database migrations");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database schema ready");

        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        if (storage is MinioFileStorageService minio)
        {
            logger.LogInformation("Ensuring MinIO bucket exists");
            await minio.EnsureBucketExistsAsync();
        }
    }

    Log.Information("ReviewInsights.Api starting up in {Environment} environment",
        app.Environment.EnvironmentName);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
