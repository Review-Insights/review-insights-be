using ReviewInsights.Api.Common;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Features.Reports.Dtos;

namespace ReviewInsights.Api.Features.Reports;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports").WithTags("Reports");

        group.MapGet("", async (ReportsService service, int? page, int? limit, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(page, limit, ct)));

        group.MapGet("{id:guid}", async (Guid id, ReportsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));

        group.MapPost("generate/preview", async (GenerateReportPayload payload, ReportsService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewGenerateAsync(payload, ct)));

        group.MapPost("generate", async (GenerateReportPayload payload, ReportsService service, CancellationToken ct) =>
        {
            var result = await service.GenerateAsync(payload, ct);
            return Results.Created($"/reports/{result.Id}", result);
        });

        group.MapDelete("{id:guid}", async (Guid id, ReportsService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        group.MapGet("{id:guid}/pdf", async (Guid id, ReportsService service, PdfReportRenderer renderer, CancellationToken ct) =>
        {
            var report = await service.GetEntityAsync(id, ct);
            if (report.Status != ReportStatus.Completed)
            {
                throw new ValidationException("PDF can be generated only for completed reports");
            }
            var bytes = renderer.Render(report);
            var safeName = string.Concat(report.Title.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            return Results.File(bytes, "application/pdf", $"{safeName}.pdf");
        });
    }
}
