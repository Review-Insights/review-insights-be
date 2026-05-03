using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Domain.ValueObjects;

public class ReportInsight
{
    public string Id { get; set; } = string.Empty;
    public InsightType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Severity { get; set; }
    public List<int> RelatedProducts { get; set; } = [];
    public List<ReportEvidence> Evidence { get; set; } = [];
    public string? TargetSegment { get; set; }
}
