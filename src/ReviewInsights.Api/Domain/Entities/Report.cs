using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Domain.Entities;

public class Report
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Generating;
    public ReportFilters Filters { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalRecords { get; set; }
    public ReportSummary? Summary { get; set; }
    public List<ReportInsight> Insights { get; set; } = [];
    public List<ReportSuggestion> Suggestions { get; set; } = [];
    public string? ErrorMessage { get; set; }
}
