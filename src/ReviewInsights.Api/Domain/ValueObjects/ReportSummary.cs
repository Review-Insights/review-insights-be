using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Domain.ValueObjects;

public class ReportSummary
{
    public int TotalReviews { get; set; }
    public double AverageRating { get; set; }
    public double RecommendationRate { get; set; }
    public Dictionary<Sentiment, int> SentimentBreakdown { get; set; } = new();
    public Dictionary<Priority, int> PriorityBreakdown { get; set; } = new();
    public List<ReportProductSummary> TopProblemProducts { get; set; } = [];
    public List<ReportProductSummary> TopOpportunityProducts { get; set; } = [];
}

public class ReportProductSummary
{
    public int ClothingId { get; set; }
    public int ReviewCount { get; set; }
    public double AverageRating { get; set; }
    public double RecommendationRate { get; set; }
    public double NegativeReviewRate { get; set; }
    public string? DepartmentName { get; set; }
    public string? ClassName { get; set; }
}
