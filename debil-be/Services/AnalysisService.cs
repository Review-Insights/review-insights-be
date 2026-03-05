using debil_be.Data;
using debil_be.DTOs;
using debil_be.Entities;
using debil_be.Messaging;
using Microsoft.EntityFrameworkCore;

namespace debil_be.Services;

public class AnalysisService(AppDbContext db, IFileStorageService fileStorage, IQueueService queue) : IAnalysisService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AnalysisStatus.Pending),
        nameof(AnalysisStatus.Processing),
        nameof(AnalysisStatus.Completed),
        nameof(AnalysisStatus.Failed)
    };

    public async Task<List<AnalysisListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Analyses
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AnalysisListItemDto
            {
                Id = a.Id,
                BlueprintName = a.BlueprintName,
                BlueprintId = a.BlueprintId,
                Filename = a.Filename,
                CreatedAt = a.CreatedAt,
                RecordCount = a.RecordCount,
                Status = a.Status.ToString()
            })
            .ToListAsync(ct);
    }

    public async Task<AnalysisDetailDto?> GetByIdAsync(Guid id, int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        var analysis = await db.Analyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (analysis is null) return null;

        var rows = await db.AnalysisRows
            .AsNoTracking()
            .Where(r => r.AnalysisId == id)
            .OrderBy(r => r.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AnalysisRowDto
            {
                Input = r.InputData,
                Output = r.OutputData
            })
            .ToListAsync(ct);

        return new AnalysisDetailDto
        {
            Id = analysis.Id,
            BlueprintId = analysis.BlueprintId,
            BlueprintName = analysis.BlueprintName,
            Filename = analysis.Filename,
            CreatedAt = analysis.CreatedAt,
            RecordCount = analysis.RecordCount,
            Status = analysis.Status.ToString(),
            InputColumns = analysis.InputColumns,
            OutputColumns = analysis.OutputColumns,
            Rows = rows
        };
    }

    public async Task<AnalysisListItemDto> CreateAsync(CreateAnalysisRequest request, Stream fileStream,
        string fileName, CancellationToken ct = default)
    {
        if (request.BlueprintId == Guid.Empty)
            throw new ArgumentException("blueprintId is required");

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName is required");

        var blueprint = await db.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BlueprintId, ct)
            ?? throw new KeyNotFoundException($"Blueprint {request.BlueprintId} not found");

        var fileKey = await fileStorage.UploadFileAsync(fileStream, fileName, "text/csv", ct);

        var analysis = new Analysis
        {
            Id = Guid.NewGuid(),
            BlueprintId = blueprint.Id,
            BlueprintName = blueprint.Name,
            Filename = fileName,
            FileStorageKey = fileKey,
            Status = AnalysisStatus.Pending,
            RecordCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        db.Analyses.Add(analysis);
        await db.SaveChangesAsync(ct);

        await queue.PublishAnalysisRequestAsync(new AnalysisRequestMessage
        {
            AnalysisId = analysis.Id,
            BlueprintId = blueprint.Id,
            FileStorageKey = fileKey
        }, ct);

        return new AnalysisListItemDto
        {
            Id = analysis.Id,
            BlueprintName = analysis.BlueprintName,
            BlueprintId = analysis.BlueprintId,
            Filename = analysis.Filename,
            CreatedAt = analysis.CreatedAt,
            RecordCount = analysis.RecordCount,
            Status = analysis.Status.ToString()
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var analysis = await db.Analyses.FindAsync([id], ct);
        if (analysis is null) return false;

        try
        {
            await fileStorage.DeleteFileAsync(analysis.FileStorageKey, ct);
        }
        catch
        {
            // File may already be deleted -- not a blocker for DB cleanup
        }

        db.Analyses.Remove(analysis);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, UpdateAnalysisStatusRequest request,
        CancellationToken ct = default)
    {
        if (!ValidStatuses.Contains(request.Status))
            throw new ArgumentException(
                $"Invalid status '{request.Status}'. Must be one of: {string.Join(", ", ValidStatuses)}");

        var analysis = await db.Analyses.FindAsync([id], ct);
        if (analysis is null) return false;

        analysis.Status = Enum.Parse<AnalysisStatus>(request.Status, ignoreCase: true);

        if (request.RecordCount.HasValue)
            analysis.RecordCount = request.RecordCount.Value;

        if (request.InputColumns is not null)
            analysis.InputColumns = request.InputColumns;

        if (request.OutputColumns is not null)
            analysis.OutputColumns = request.OutputColumns;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AddRowsAsync(Guid id, AddAnalysisRowsRequest request, CancellationToken ct = default)
    {
        var analysisExists = await db.Analyses.AnyAsync(a => a.Id == id, ct);
        if (!analysisExists) return false;

        if (request.Rows.Count == 0)
            throw new ArgumentException("At least one row is required");

        var currentMaxIndex = await db.AnalysisRows
            .Where(r => r.AnalysisId == id)
            .MaxAsync(r => (int?)r.RowIndex, ct) ?? -1;

        var entities = request.Rows.Select((row, i) => new AnalysisRow
        {
            Id = Guid.NewGuid(),
            AnalysisId = id,
            RowIndex = currentMaxIndex + 1 + i,
            InputData = row.Input,
            OutputData = row.Output
        }).ToList();

        db.AnalysisRows.AddRange(entities);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
