using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Messaging;

public class GenerateReportMessage
{
    public string TaskType { get; set; } = "generate_report";
    public Guid ReportId { get; set; }
    public ReportFilters Filters { get; set; } = new();
    public List<AnalyzedReviewPayload> Reviews { get; set; } = [];
}

public class AnalyzedReviewPayload
{
    public Guid Id { get; set; }
    public int ClothingId { get; set; }
    public int Age { get; set; }
    public string? Title { get; set; }
    public string? ReviewText { get; set; }
    public int Rating { get; set; }
    public bool RecommendedInd { get; set; }
    public string? DivisionName { get; set; }
    public string? DepartmentName { get; set; }
    public string? ClassName { get; set; }
    public Sentiment? OverallSentiment { get; set; }
    public List<AspectSentiment> AspectSentiments { get; set; } = [];
    public Priority? Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AnalyzedAt { get; set; }
}
