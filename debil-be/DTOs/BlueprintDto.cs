namespace debil_be.DTOs;

public class BlueprintDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> DataStructure { get; set; } = new();
    public List<BlueprintTaskDto> Tasks { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BlueprintListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateBlueprintRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> DataStructure { get; set; } = new();
    public List<CreateBlueprintTaskRequest> Tasks { get; set; } = [];
}

public class UpdateBlueprintRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> DataStructure { get; set; } = new();
    public List<CreateBlueprintTaskRequest> Tasks { get; set; } = [];
}
