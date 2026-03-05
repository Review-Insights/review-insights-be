namespace debil_be.Entities;

public class Blueprint
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> DataStructure { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<BlueprintTask> Tasks { get; set; } = [];
    public List<Analysis> Analyses { get; set; } = [];
}
