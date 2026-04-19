using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Domain.ValueObjects;

public class ReportInsight
{
    public Guid Id { get; set; }
    public InsightType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Severity { get; set; }
}
