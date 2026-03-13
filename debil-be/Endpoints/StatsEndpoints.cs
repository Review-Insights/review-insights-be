using debil_be.Data;
using debil_be.DTOs;
using Microsoft.EntityFrameworkCore;

namespace debil_be.Endpoints;

public static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stats")
            .WithTags("Stats");

        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            var activeBlueprints = await db.Blueprints
                .AsNoTracking()
                .CountAsync(ct);

            var analysesToday = await db.Analyses
                .AsNoTracking()
                .CountAsync(a => a.CreatedAt >= today, ct);

            var completedCount = await db.Analyses
                .AsNoTracking()
                .CountAsync(a => a.Status == Entities.AnalysisStatus.Completed, ct);

            var failedCount = await db.Analyses
                .AsNoTracking()
                .CountAsync(a => a.Status == Entities.AnalysisStatus.Failed, ct);

            var totalWithOutcome = completedCount + failedCount;
            var successRate = totalWithOutcome == 0
                ? 0
                : (double)completedCount / totalWithOutcome * 100.0;

            var avgConfidence = await db.TaskMetrics
                .AsNoTracking()
                .AverageAsync(m => (double?)m.AvgConfidence, ct) ?? 0;

            var stats = new List<StatCardDataDto>
            {
                new()
                {
                    Key = StatCardKey.active_blueprints,
                    Title = "Aktywne blueprinty",
                    Value = activeBlueprints.ToString(),
                    Change = "—",
                    Trend = "up"
                },
                new()
                {
                    Key = StatCardKey.analyses_today,
                    Title = "Analizy dzisiaj",
                    Value = analysesToday.ToString(),
                    Change = "—",
                    Trend = "up"
                },
                new()
                {
                    Key = StatCardKey.success_rate,
                    Title = "Skuteczność analiz",
                    Value = $"{successRate:F1}%",
                    Change = "—",
                    Trend = "up"
                },
                new()
                {
                    Key = StatCardKey.avg_processing_time,
                    Title = "Średnia pewność modeli",
                    Value = avgConfidence > 0 ? $"{avgConfidence:F1}%" : "brak danych",
                    Change = "—",
                    Trend = avgConfidence > 70 ? "up" : "down"
                }
            };

            var response = new StatsApiResponseDto { Stats = stats };
            return Results.Ok(response);
        }).WithName("GetStats");

        group.MapGet("/tasks", async (AppDbContext db, CancellationToken ct) =>
        {
            var taskStats = await db.TaskMetrics
                .AsNoTracking()
                .GroupBy(m => new { m.TaskType, m.TaskName })
                .Select(g => new TaskTypeStatsDto
                {
                    TaskType = g.Key.TaskType,
                    TaskName = g.Key.TaskName,
                    TotalRuns = g.Count(),
                    AvgConfidence = g.Average(m => m.AvgConfidence),
                    AvgAccuracy = g.Where(m => m.Accuracy != null).Average(m => m.Accuracy),
                    AvgF1 = g.Where(m => m.F1Score != null).Average(m => m.F1Score),
                    AvgPrecision = g.Where(m => m.Precision != null).Average(m => m.Precision),
                    AvgRecall = g.Where(m => m.Recall != null).Average(m => m.Recall),
                    AvgAuc = g.Where(m => m.AucRoc != null).Average(m => m.AucRoc),
                    TotalRecordsProcessed = g.Sum(m => m.RecordCount)
                })
                .OrderByDescending(t => t.TotalRuns)
                .ToListAsync(ct);

            return Results.Ok(new TaskTypeStatsResponseDto { Tasks = taskStats });
        }).WithName("GetTaskStats");

        return group;
    }
}
