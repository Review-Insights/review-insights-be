using ReviewInsights.Api.Features.Reviews;

namespace ReviewInsights.Api.Features.Products;

public static class ProductsEndpoints
{
    public static void MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        group.MapGet("", async (
            ProductsService service,
            int? page, int? limit, string? search, string? departmentName, string? divisionName,
            string? sortBy, string? sortOrder, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(page, limit, search, departmentName, divisionName, sortBy, sortOrder, ct)));

        group.MapGet("{clothingId:int}", async (int clothingId, ProductsService service, CancellationToken ct) =>
            Results.Ok(await service.GetDetailAsync(clothingId, ct)));

        group.MapGet("{clothingId:int}/reviews", async (
            int clothingId,
            [AsParameters] ReviewFilterParams filters,
            ReviewsService reviewsService,
            CancellationToken ct) =>
        {
            filters.ClothingId = clothingId;
            return Results.Ok(await reviewsService.ListAsync(filters, ct));
        });

        group.MapGet("{clothingId:int}/trends", async (int clothingId, ProductsService service, CancellationToken ct) =>
            Results.Ok(new { data = await service.GetTrendsAsync(clothingId, ct) }));

        group.MapGet("{clothingId:int}/aspects", async (int clothingId, ProductsService service, CancellationToken ct) =>
            Results.Ok(new { data = await service.GetAspectsAsync(clothingId, ct) }));
    }
}
