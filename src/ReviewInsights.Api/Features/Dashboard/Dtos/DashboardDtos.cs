namespace ReviewInsights.Api.Features.Dashboard.Dtos;

public class DashboardStatCardDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Trend { get; set; } = "neutral";
    public string Icon { get; set; } = string.Empty;
    public string? Period { get; set; }
}

public class SentimentTrendPointDto
{
    public string Period { get; set; } = string.Empty;
    public int Positive { get; set; }
    public int Neutral { get; set; }
    public int Negative { get; set; }
}

public class RatingDistributionItemDto
{
    public int Rating { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class DepartmentStatItemDto
{
    public string Department { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public double RecommendationRate { get; set; }
    public double AverageSentiment { get; set; }
}
