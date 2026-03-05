namespace debil_be.Entities;

public class Analysis
{
    public Guid Id { get; set; }
    public Guid BlueprintId { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string FileStorageKey { get; set; } = string.Empty;
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public int RecordCount { get; set; }
    public List<ColumnMeta>? InputColumns { get; set; }
    public List<OutputColumnMeta>? OutputColumns { get; set; }
    public DateTime CreatedAt { get; set; }

    public Blueprint Blueprint { get; set; } = null!;
    public List<AnalysisRow> Rows { get; set; } = [];
}

public enum AnalysisStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class ColumnMeta
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class OutputColumnMeta
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
}
