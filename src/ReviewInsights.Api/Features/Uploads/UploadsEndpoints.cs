namespace ReviewInsights.Api.Features.Uploads;

public static class UploadsEndpoints
{
    public static void MapUploadsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/uploads").WithTags("Uploads");

        group.MapGet("", async (
            UploadsService service,
            int? page, int? limit, string? status, string? sortBy, string? sortOrder,
            CancellationToken ct) =>
                Results.Ok(await service.ListAsync(page, limit, status, sortBy, sortOrder, ct)));

        group.MapPost("", async (HttpRequest request, UploadsService service, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Request must be multipart/form-data" });
            }
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return Results.BadRequest(new { error = "No file provided" });
            }
            var result = await service.UploadAsync(file, ct);
            return Results.Created($"/uploads/{result.Id}", result);
        }).DisableAntiforgery();

        group.MapDelete("{id:guid}", async (Guid id, UploadsService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
