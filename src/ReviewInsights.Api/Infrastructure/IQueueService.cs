using ReviewInsights.Api.Messaging;

namespace ReviewInsights.Api.Infrastructure;

public interface IQueueService
{
    Task PublishAnalyzeReviewsAsync(AnalyzeReviewsMessage message, CancellationToken ct = default);
    Task PublishGenerateReportAsync(GenerateReportMessage message, CancellationToken ct = default);
}
