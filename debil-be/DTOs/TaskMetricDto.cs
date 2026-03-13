namespace debil_be.DTOs;

public class TaskMetricDto
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

public class AnalysisMetricsDto
{
    public Guid AnalysisId { get; set; }
    public double? OverallAvgConfidence { get; set; }
    public double? OverallAccuracy { get; set; }
    public List<TaskMetricDto> TaskMetrics { get; set; } = [];
}

public class SaveTaskMetricsRequest
{
    public double? OverallAvgConfidence { get; set; }
    public double? OverallAccuracy { get; set; }
    public List<SaveTaskMetricItem> Tasks { get; set; } = [];
}

public class SaveTaskMetricItem
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
