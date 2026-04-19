using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Features.Products.Dtos;

public class ProductDto
{
    public int ClothingId { get; set; }
    public string? ClassName { get; set; }
    public string? DepartmentName { get; set; }
    public string? DivisionName { get; set; }
    public int TotalReviews { get; set; }
    public double AverageRating { get; set; }
    public double RecommendationRate { get; set; }
    public double AverageSentiment { get; set; }
    public Priority Priority { get; set; } = Priority.Low;
}

public class ProductDetailDto : ProductDto
{
    public Dictionary<string, int> SentimentDistribution { get; set; } = new();
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    public List<ProductAspectDataDto> AspectSentiments { get; set; } = [];
    public List<ProductTrendPointDto> RecentTrends { get; set; } = [];
}

public class ProductAspectDataDto
{
    public AspectKey Aspect { get; set; }
    public double AverageScore { get; set; }
    public Dictionary<string, int> Distribution { get; set; } = new();
}

public class ProductTrendPointDto
{
    public string Period { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public double AverageSentiment { get; set; }
    public int ReviewCount { get; set; }
}
