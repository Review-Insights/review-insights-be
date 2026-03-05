using debil_be.Entities;

namespace debil_be.DTOs;

public class BlueprintTaskDto
{
    public Guid Id { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Question { get; set; }
    public string? Instruction { get; set; }
    public List<TaskValue>? Values { get; set; }
    public string? Format { get; set; }
    public int? MaxLength { get; set; }
    public double? Temperature { get; set; }
    public string? Model { get; set; }
}

public class CreateBlueprintTaskRequest
{
    public string TaskType { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Question { get; set; }
    public string? Instruction { get; set; }
    public List<TaskValue>? Values { get; set; }
    public string? Format { get; set; }
    public int? MaxLength { get; set; }
    public double? Temperature { get; set; }
    public string? Model { get; set; }
}
