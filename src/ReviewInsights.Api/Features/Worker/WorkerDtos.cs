using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Features.Worker;

public class WorkerReviewResultDto
{
    public Guid ReviewId { get; set; }
    public Sentiment OverallSentiment { get; set; }
    public List<AspectSentiment> AspectSentiments { get; set; } = [];
    public Priority Priority { get; set; }
    public string? PriorityRule { get; set; }
    public string? PriorityReason { get; set; }
}

public class WorkerAnalyzeResultsRequest
{
    public List<WorkerReviewResultDto> Results { get; set; } = [];
}

public class WorkerReportResultRequest
{
    public ReportScope Scope { get; set; } = new();
    public ReportSummary Summary { get; set; } = new();
    public List<ReportInsight> Insights { get; set; } = [];
    public List<ReportSuggestion> Suggestions { get; set; } = [];
}

public class WorkerErrorRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
}
