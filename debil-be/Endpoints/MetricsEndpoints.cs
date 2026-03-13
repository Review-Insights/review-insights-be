using debil_be.Data;
using debil_be.DTOs;
using debil_be.Entities;
using Microsoft.EntityFrameworkCore;

namespace debil_be.Endpoints;

public static class MetricsEndpoints
{
    public static RouteGroupBuilder MapMetricsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analyses/{analysisId:guid}/metrics")
            .WithTags("Metrics");

        group.MapGet("/", async (Guid analysisId, AppDbContext db, CancellationToken ct) =>
        {
            var analysis = await db.Analyses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == analysisId, ct);

            if (analysis is null) return Results.NotFound();

            var metrics = await db.TaskMetrics
                .AsNoTracking()
                .Where(m => m.AnalysisId == analysisId)
                .OrderBy(m => m.TaskName)
                .Select(m => new TaskMetricDto
                {
                    TaskId = m.TaskId,
                    TaskType = m.TaskType,
                    TaskName = m.TaskName,
                    ModelName = m.ModelName,
                    RecordCount = m.RecordCount,
                    AvgConfidence = m.AvgConfidence,
                    MinConfidence = m.MinConfidence,
                    MaxConfidence = m.MaxConfidence,
                    Accuracy = m.Accuracy,
                    Precision = m.Precision,
                    Recall = m.Recall,
                    F1Score = m.F1Score,
                    AucRoc = m.AucRoc,
                    Support = m.Support
                })
                .ToListAsync(ct);

            var overallAvgConfidence = metrics.Count > 0
                ? metrics.Average(m => m.AvgConfidence)
                : (double?)null;

            var overallAccuracy = metrics.Any(m => m.Accuracy.HasValue)
                ? metrics.Where(m => m.Accuracy.HasValue).Average(m => m.Accuracy!.Value)
                : (double?)null;

            return Results.Ok(new AnalysisMetricsDto
            {
                AnalysisId = analysisId,
                OverallAvgConfidence = overallAvgConfidence,
                OverallAccuracy = overallAccuracy,
                TaskMetrics = metrics
            });
        }).WithName("GetAnalysisMetrics");

        group.MapPost("/", async (
            Guid analysisId,
            SaveTaskMetricsRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var analysisExists = await db.Analyses.AnyAsync(a => a.Id == analysisId, ct);
            if (!analysisExists) return Results.NotFound();

            // Remove existing metrics for idempotency
            var existing = await db.TaskMetrics
                .Where(m => m.AnalysisId == analysisId)
                .ToListAsync(ct);
            db.TaskMetrics.RemoveRange(existing);

            var entities = request.Tasks.Select(t => new TaskMetric
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                TaskId = t.TaskId,
                TaskType = t.TaskType,
                TaskName = t.TaskName,
                ModelName = t.ModelName,
                RecordCount = t.RecordCount,
                AvgConfidence = t.AvgConfidence,
                MinConfidence = t.MinConfidence,
                MaxConfidence = t.MaxConfidence,
                Accuracy = t.Accuracy,
                Precision = t.Precision,
                Recall = t.Recall,
                F1Score = t.F1Score,
                AucRoc = t.AucRoc,
                Support = t.Support,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            db.TaskMetrics.AddRange(entities);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("SaveAnalysisMetrics")
        .WithTags("Metrics - Worker");

        return group;
    }
}
