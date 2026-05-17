namespace ReviewInsights.Api.Features.History;

public class HistoryScopeParams
{
    public int? ClothingId { get; set; }
    public string? ClassName { get; set; }
    public string? DivisionName { get; set; }
}

public class HistorySectionDto
{
    public Dictionary<string, Dictionary<int, int>> AspectCounts { get; set; } = new();
    public Dictionary<string, Dictionary<int, int>> CauseCounts { get; set; } = new();
    public Dictionary<int, int> VeryNegativeCounts { get; set; } = new();
    public Dictionary<int, double> RecommendationRate { get; set; } = new();
    public Dictionary<int, double> NegativeSentimentRate { get; set; } = new();
    public double? AvgPositiveFeedback { get; set; }
}

public class HistorySnapshotDto
{
    public HistorySectionDto Product { get; set; } = new();
    public HistorySectionDto Class { get; set; } = new();
    public HistorySectionDto Segment { get; set; } = new();
}
