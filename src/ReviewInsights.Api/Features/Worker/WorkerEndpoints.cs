using ReviewInsights.Api.Common;

namespace ReviewInsights.Api.Features.Worker;

public static class WorkerEndpoints
{
    public static void MapWorkerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/worker").WithTags("Worker");

        group.MapPost("uploads/{uploadId:guid}/results", async (
            Guid uploadId, WorkerAnalyzeResultsRequest request, WorkerService service, CancellationToken ct) =>
        {
            await service.PatchAnalyzeResultsAsync(uploadId, request, ct);
            return Results.Accepted();
        });

        group.MapPost("reports/{reportId:guid}/result", async (
            Guid reportId, WorkerReportResultRequest request, WorkerService service, CancellationToken ct) =>
        {
            await service.PatchReportResultAsync(reportId, request, ct);
            return Results.Accepted();
        });

        group.MapPost("uploads/{uploadId:guid}/error", async (
            Guid uploadId, WorkerErrorRequest request, WorkerService service, CancellationToken ct) =>
        {
            await service.RegisterUploadErrorAsync(uploadId, request, ct);
            return Results.Accepted();
        });

        group.MapPost("reports/{reportId:guid}/error", async (
            Guid reportId, WorkerErrorRequest request, WorkerService service, CancellationToken ct) =>
        {
            await service.RegisterReportErrorAsync(reportId, request, ct);
            return Results.Accepted();
        });

        group.MapPost("{resource}/{id:guid}/error", (string resource, Guid id) =>
            Results.BadRequest(ErrorResponse.From($"Unknown resource '{resource}'. Allowed: uploads, reports")));
    }
}
