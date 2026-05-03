using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Features.Reports.Dtos;

public class ReportListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public ReportFilters Filters { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalRecords { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ReportDetailDto : ReportListItemDto
{
    public ReportScope? Scope { get; set; }
    public ReportSummary? Summary { get; set; }
    public List<ReportInsight> Insights { get; set; } = [];
    public List<ReportSuggestion> Suggestions { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public class GenerateReportPayload
{
    public string Title { get; set; } = string.Empty;
    public ReportFilters Filters { get; set; } = new();
}

public class GenerateReportPreviewDto
{
    public int TotalMatchingRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SkippedRecords { get; set; }
    public int MaxRecordsLimit { get; set; }
    public bool CanGenerate { get; set; }
    public string? Message { get; set; }
}
