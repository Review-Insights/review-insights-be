using debil_be.DTOs;
using debil_be.Services;

namespace debil_be.Endpoints;

public static class AnalysisEndpoints
{
    public static RouteGroupBuilder MapAnalysisEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analyses")
            .WithTags("Analyses");

        group.MapGet("/", async (IAnalysisService service, CancellationToken ct) =>
        {
            var analyses = await service.GetAllAsync(ct);
            return Results.Ok(analyses);
        }).WithName("GetAnalyses");

        group.MapGet("/{id:guid}",
            async (Guid id, int? page, int? pageSize, IAnalysisService service, CancellationToken ct) =>
            {
                var analysis = await service.GetByIdAsync(id, page ?? 1, pageSize ?? 50, ct);
                return analysis is null ? Results.NotFound() : Results.Ok(analysis);
            }).WithName("GetAnalysisById");

        group.MapPost("/", async (HttpRequest httpRequest, IAnalysisService service, CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data");

            var form = await httpRequest.ReadFormAsync(ct);

            var blueprintIdStr = form["blueprintId"].FirstOrDefault();
            if (string.IsNullOrEmpty(blueprintIdStr) || !Guid.TryParse(blueprintIdStr, out var blueprintId))
                return Results.BadRequest("blueprintId is required and must be a valid GUID");

            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest("CSV file is required");

            await using var stream = file.OpenReadStream();
            var request = new CreateAnalysisRequest { BlueprintId = blueprintId };

            var result = await service.CreateAsync(request, stream, file.FileName, ct);
            return Results.Accepted($"/api/analyses/{result.Id}", result);
        })
        .DisableAntiforgery()
        .WithName("CreateAnalysis");

        group.MapDelete("/{id:guid}", async (Guid id, IAnalysisService service, CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteAnalysis");

        group.MapPut("/{id:guid}/status",
            async (Guid id, UpdateAnalysisStatusRequest request, IAnalysisService service, CancellationToken ct) =>
            {
                var updated = await service.UpdateStatusAsync(id, request, ct);
                return updated ? Results.NoContent() : Results.NotFound();
            })
            .WithName("UpdateAnalysisStatus")
            .WithTags("Analyses - Worker");

        group.MapPost("/{id:guid}/rows",
            async (Guid id, AddAnalysisRowsRequest request, IAnalysisService service, CancellationToken ct) =>
            {
                var added = await service.AddRowsAsync(id, request, ct);
                return added ? Results.NoContent() : Results.NotFound();
            })
            .WithName("AddAnalysisRows")
            .WithTags("Analyses - Worker");

        return group;
    }
}
