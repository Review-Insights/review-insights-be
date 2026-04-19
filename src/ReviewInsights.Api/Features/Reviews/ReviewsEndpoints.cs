namespace ReviewInsights.Api.Features.Reviews;

public static class ReviewsEndpoints
{
    public static void MapReviewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reviews").WithTags("Reviews");

        group.MapGet("", async ([AsParameters] ReviewFilterParams filters, ReviewsService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(filters, ct)));

        group.MapGet("{id:guid}", async (Guid id, ReviewsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));
    }
}
