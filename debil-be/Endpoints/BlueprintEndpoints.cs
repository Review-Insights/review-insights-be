using debil_be.DTOs;
using debil_be.Services;

namespace debil_be.Endpoints;

public static class BlueprintEndpoints
{
    public static RouteGroupBuilder MapBlueprintEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/blueprints")
            .WithTags("Blueprints");

        group.MapGet("/", async (IBlueprintService service, CancellationToken ct) =>
        {
            var blueprints = await service.GetAllAsync(ct);
            return Results.Ok(blueprints);
        }).WithName("GetBlueprints");

        group.MapGet("/{id:guid}", async (Guid id, IBlueprintService service, CancellationToken ct) =>
        {
            var blueprint = await service.GetByIdAsync(id, ct);
            return blueprint is null ? Results.NotFound() : Results.Ok(blueprint);
        }).WithName("GetBlueprintById");

        group.MapPost("/", async (CreateBlueprintRequest request, IBlueprintService service, CancellationToken ct) =>
        {
            var blueprint = await service.CreateAsync(request, ct);
            return Results.Created($"/api/blueprints/{blueprint.Id}", blueprint);
        }).WithName("CreateBlueprint");

        group.MapPut("/{id:guid}",
            async (Guid id, UpdateBlueprintRequest request, IBlueprintService service, CancellationToken ct) =>
            {
                var blueprint = await service.UpdateAsync(id, request, ct);
                return blueprint is null ? Results.NotFound() : Results.Ok(blueprint);
            }).WithName("UpdateBlueprint");

        group.MapDelete("/{id:guid}", async (Guid id, IBlueprintService service, CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteBlueprint");

        return group;
    }
}
