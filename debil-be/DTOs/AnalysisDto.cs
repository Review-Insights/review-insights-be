using debil_be.Entities;

namespace debil_be.DTOs;

public class AnalysisDetailDto
{
    public Guid Id { get; set; }
    public Guid BlueprintId { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int RecordCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ColumnMeta>? InputColumns { get; set; }
    public List<OutputColumnMeta>? OutputColumns { get; set; }
    public List<AnalysisRowDto> Rows { get; set; } = [];
}

public class AnalysisListItemDto
{
    public Guid Id { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public Guid BlueprintId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int RecordCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AnalysisRowDto
{
    public Dictionary<string, object?> Input { get; set; } = new();
    public Dictionary<string, object?> Output { get; set; } = new();
}

public class CreateAnalysisRequest
{
    public Guid BlueprintId { get; set; }
}

public class UpdateAnalysisStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public int? RecordCount { get; set; }
    public List<ColumnMeta>? InputColumns { get; set; }
    public List<OutputColumnMeta>? OutputColumns { get; set; }
}

public class AddAnalysisRowsRequest
{
    public List<AnalysisRowDto> Rows { get; set; } = [];
}
