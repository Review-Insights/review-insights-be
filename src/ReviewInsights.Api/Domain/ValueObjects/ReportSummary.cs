using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Domain.ValueObjects;

public class ReportSummary
{
    public double AverageRating { get; set; }
    public double RecommendationRate { get; set; }
    public Dictionary<Sentiment, int> SentimentBreakdown { get; set; } = new();
    public List<ChurnCauseCount> TopChurnCauses { get; set; } = [];
}

public class ChurnCauseCount
{
    public ChurnCause Cause { get; set; }
    public int Count { get; set; }
}
