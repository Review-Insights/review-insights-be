namespace ReviewInsights.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dashboard").WithTags("Dashboard");

        group.MapGet("stats", async (DashboardService service, CancellationToken ct) =>
            Results.Ok(new { stats = await service.GetStatsAsync(ct) }));

        group.MapGet("sentiment-trend", async (string? period, DashboardService service, CancellationToken ct) =>
            Results.Ok(new { data = await service.GetSentimentTrendAsync(period, ct) }));

        group.MapGet("rating-distribution", async (DashboardService service, CancellationToken ct) =>
            Results.Ok(new { data = await service.GetRatingDistributionAsync(ct) }));

        group.MapGet("department-stats", async (DashboardService service, CancellationToken ct) =>
            Results.Ok(new { data = await service.GetDepartmentStatsAsync(ct) }));
    }
}
