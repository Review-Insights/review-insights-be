using debil_be.DTOs;

namespace debil_be.Services;

public interface IBlueprintService
{
    Task<List<BlueprintListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<BlueprintDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BlueprintDto> CreateAsync(CreateBlueprintRequest request, CancellationToken ct = default);
    Task<BlueprintDto?> UpdateAsync(Guid id, UpdateBlueprintRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
