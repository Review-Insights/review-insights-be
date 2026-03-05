using debil_be.Messaging;

namespace debil_be.Services;

public interface IQueueService
{
    Task PublishAnalysisRequestAsync(AnalysisRequestMessage message, CancellationToken ct = default);
}
