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
using ReviewInsights.Api.Features.Products;
using ReviewInsights.Api.Features.Reports;
using ReviewInsights.Api.Features.Reviews;
using ReviewInsights.Api.Features.Uploads;
using ReviewInsights.Api.Features.Worker;
using ReviewInsights.Api.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
            logger.LogError(exception, "Unhandled exception");
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ErrorResponse.From(message));
    });
});

app.UseStatusCodePages();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
    if (storage is MinioFileStorageService minio)
    {
        await minio.EnsureBucketExistsAsync();
    }
}

await app.RunAsync();
