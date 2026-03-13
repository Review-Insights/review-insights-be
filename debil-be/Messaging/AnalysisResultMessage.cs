using debil_be.Entities;

namespace debil_be.Messaging;

/// <summary>
/// Payload sent by Kedro worker to BE via RabbitMQ results queue.
/// Same logical content as HTTP calls from agent_worker (status + rows + metrics).
/// </summary>
public class AnalysisResultMessage
{
    public string AnalysisId { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public string? Error { get; set; }
    public List<AnalysisRowMessage> Rows { get; set; } = [];
    public AnalysisMetricsMessage? Metrics { get; set; }
    public List<ColumnMeta>? InputColumns { get; set; }
    public List<OutputColumnMeta>? OutputColumns { get; set; }
}

public class AnalysisRowMessage
{
    public Dictionary<string, object?> Input { get; set; } = new();
    public Dictionary<string, object?> Output { get; set; } = new();
}

public class AnalysisMetricsMessage
{
    public double? OverallAvgConfidence { get; set; }
    public double? OverallAccuracy { get; set; }
    public List<SaveTaskMetricItemMessage> Tasks { get; set; } = [];
}

public class SaveTaskMetricItemMessage
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public int RecordCount { get; set; }
    public double AvgConfidence { get; set; }
    public double MinConfidence { get; set; }
    public double MaxConfidence { get; set; }
    public double? Accuracy { get; set; }
    public double? Precision { get; set; }
    public double? Recall { get; set; }
    public double? F1Score { get; set; }
    public double? AucRoc { get; set; }
    public int? Support { get; set; }
}
