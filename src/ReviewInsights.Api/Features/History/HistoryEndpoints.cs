namespace ReviewInsights.Api.Features.History;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/history").WithTags("History");

        group.MapGet(
            "snapshot",
            async ([AsParameters] HistoryScopeParams scope, HistoryService service, CancellationToken ct) =>
                Results.Ok(await service.GetSnapshotAsync(scope, ct)));
    }
}
