using System.Text.Json.Serialization;
using debil_be.Configuration;
using debil_be.Data;
using debil_be.Endpoints;
using debil_be.Infrastructure;
using debil_be.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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
builder.Services.AddSingleton<AnalysisResultConsumer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AnalysisResultConsumer>());

builder.Services.AddScoped<IBlueprintService, BlueprintService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

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

        var (statusCode, message) = exception switch
        {
            ArgumentException ex => (StatusCodes.Status400BadRequest, ex.Message),
            KeyNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message, status = statusCode });
    });
});

app.UseStatusCodePages();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "debil-be API");
    });
    app.MapScalarApiReference();
    app.MapDevEndpoints();
}

app.UseCors();

app.MapHealthChecks("/health");
app.MapBlueprintEndpoints();
app.MapAnalysisEndpoints();
app.MapStatsEndpoints();
app.MapMetricsEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
