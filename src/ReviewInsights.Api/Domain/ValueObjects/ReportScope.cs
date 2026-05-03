namespace ReviewInsights.Api.Domain.ValueObjects;

public class ReportScope
{
    public ReportFilters Filters { get; set; } = new();
    public int AnalyzedReviewCount { get; set; }
    public int SkippedReviewCount { get; set; }
}
