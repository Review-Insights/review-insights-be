namespace debil_be.DTOs;

public enum StatCardKey
{
    active_blueprints,
    analyses_today,
    success_rate,
    avg_processing_time
}

public class StatCardDataDto
{
    public StatCardKey Key { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Trend { get; set; } = "up";
}

public class StatsApiResponseDto
{
    public List<StatCardDataDto> Stats { get; set; } = [];
}

public class TaskTypeStatsDto
{
    public string TaskType { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public int TotalRuns { get; set; }
    public double AvgConfidence { get; set; }
    public double? AvgAccuracy { get; set; }
    public double? AvgF1 { get; set; }
    public double? AvgPrecision { get; set; }
    public double? AvgRecall { get; set; }
    public double? AvgAuc { get; set; }
    public int TotalRecordsProcessed { get; set; }
}

public class TaskTypeStatsResponseDto
{
    public List<TaskTypeStatsDto> Tasks { get; set; } = [];
}

