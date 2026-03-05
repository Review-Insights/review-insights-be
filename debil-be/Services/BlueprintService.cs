using debil_be.Data;
using debil_be.DTOs;
using debil_be.Entities;
using Microsoft.EntityFrameworkCore;

namespace debil_be.Services;

public class BlueprintService(AppDbContext db) : IBlueprintService
{
    private static readonly HashSet<string> ValidTaskTypes =
    [
        "classification", "extraction", "generation", "multi_select", "boolean"
    ];

    public async Task<List<BlueprintListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Blueprints
            .AsNoTracking()
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => new BlueprintListItemDto
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                TaskCount = b.Tasks.Count,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<BlueprintDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var blueprint = await db.Blueprints
            .AsNoTracking()
            .Include(b => b.Tasks.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (blueprint is null) return null;

        return MapToDto(blueprint);
    }

    public async Task<BlueprintDto> CreateAsync(CreateBlueprintRequest request, CancellationToken ct = default)
    {
        ValidateBlueprint(request.Name, request.Tasks);

        var now = DateTime.UtcNow;
        var blueprint = new Blueprint
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            DataStructure = request.DataStructure,
            CreatedAt = now,
            UpdatedAt = now,
            Tasks = request.Tasks.Select((t, index) => new BlueprintTask
            {
                Id = Guid.NewGuid(),
                TaskType = t.TaskType,
                TaskName = t.TaskName,
                Description = t.Description,
                Question = t.Question,
                Instruction = t.Instruction,
                Values = t.Values,
                Format = t.Format,
                MaxLength = t.MaxLength,
                Temperature = t.Temperature,
                Model = t.Model,
                SortOrder = index
            }).ToList()
        };

        db.Blueprints.Add(blueprint);
        await db.SaveChangesAsync(ct);

        return MapToDto(blueprint);
    }

    public async Task<BlueprintDto?> UpdateAsync(Guid id, UpdateBlueprintRequest request,
        CancellationToken ct = default)
    {
        ValidateBlueprint(request.Name, request.Tasks);

        var blueprint = await db.Blueprints
            .Include(b => b.Tasks)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (blueprint is null) return null;

        blueprint.Name = request.Name;
        blueprint.Description = request.Description;
        blueprint.DataStructure = request.DataStructure;
        blueprint.UpdatedAt = DateTime.UtcNow;

        db.BlueprintTasks.RemoveRange(blueprint.Tasks);

        blueprint.Tasks = request.Tasks.Select((t, index) => new BlueprintTask
        {
            Id = Guid.NewGuid(),
            BlueprintId = blueprint.Id,
            TaskType = t.TaskType,
            TaskName = t.TaskName,
            Description = t.Description,
            Question = t.Question,
            Instruction = t.Instruction,
            Values = t.Values,
            Format = t.Format,
            MaxLength = t.MaxLength,
            Temperature = t.Temperature,
            Model = t.Model,
            SortOrder = index
        }).ToList();

        await db.SaveChangesAsync(ct);

        return MapToDto(blueprint);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var blueprint = await db.Blueprints.FindAsync([id], ct);
        if (blueprint is null) return false;

        db.Blueprints.Remove(blueprint);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidateBlueprint(string name, List<CreateBlueprintTaskRequest> tasks)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Blueprint name is required");

        if (tasks.Count == 0)
            throw new ArgumentException("At least one task is required");

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];

            if (string.IsNullOrWhiteSpace(task.TaskName))
                throw new ArgumentException($"Task #{i + 1}: task_name is required");

            if (!ValidTaskTypes.Contains(task.TaskType))
                throw new ArgumentException(
                    $"Task '{task.TaskName}': invalid task_type '{task.TaskType}'. " +
                    $"Must be one of: {string.Join(", ", ValidTaskTypes)}");
        }
    }

    private static BlueprintDto MapToDto(Blueprint blueprint) => new()
    {
        Id = blueprint.Id,
        Name = blueprint.Name,
        Description = blueprint.Description,
        DataStructure = blueprint.DataStructure,
        CreatedAt = blueprint.CreatedAt,
        UpdatedAt = blueprint.UpdatedAt,
        Tasks = blueprint.Tasks.Select(t => new BlueprintTaskDto
        {
            Id = t.Id,
            TaskType = t.TaskType,
            TaskName = t.TaskName,
            Description = t.Description,
            Question = t.Question,
            Instruction = t.Instruction,
            Values = t.Values,
            Format = t.Format,
            MaxLength = t.MaxLength,
            Temperature = t.Temperature,
            Model = t.Model
        }).ToList()
    };
}
