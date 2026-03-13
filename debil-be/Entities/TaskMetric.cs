namespace debil_be.Entities;

public class TaskMetric
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
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
    public DateTime CreatedAt { get; set; }

    public Analysis Analysis { get; set; } = null!;
}
