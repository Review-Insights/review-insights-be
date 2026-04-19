using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Domain.ValueObjects;

public class ReportSuggestion
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public List<int> RelatedProducts { get; set; } = [];
}
