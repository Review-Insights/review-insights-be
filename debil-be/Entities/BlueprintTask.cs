namespace debil_be.Entities;

public class BlueprintTask
{
    public Guid Id { get; set; }
    public Guid BlueprintId { get; set; }
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
    public int SortOrder { get; set; }

    public Blueprint Blueprint { get; set; } = null!;
}

public class TaskValue
{
    public string Value { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = [];
}
