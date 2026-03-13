using debil_be.DTOs;

namespace debil_be.Services;

public interface IAnalysisService
{
    Task<List<AnalysisListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<AnalysisDetailDto?> GetByIdAsync(Guid id, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<AnalysisListItemDto> CreateAsync(CreateAnalysisRequest request, Stream fileStream, string fileName,
        CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(Guid id, UpdateAnalysisStatusRequest request, CancellationToken ct = default);
    Task<bool> AddRowsAsync(Guid id, AddAnalysisRowsRequest request, CancellationToken ct = default);
    Task<bool> SaveTaskMetricsAsync(Guid id, SaveTaskMetricsRequest request, CancellationToken ct = default);
}
