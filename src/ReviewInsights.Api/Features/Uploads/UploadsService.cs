using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Configuration;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Entities;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Features.Uploads.Dtos;
using ReviewInsights.Api.Infrastructure;
using ReviewInsights.Api.Messaging;

namespace ReviewInsights.Api.Features.Uploads;

public class UploadsService
{
    private const long MaxFileSizeBytes = 52_428_800; // 50 MB
    private static readonly string[] AllowedExtensions = [".csv", ".json"];

    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IQueueService _queue;
    private readonly CsvJsonReviewParser _parser;
    private readonly RabbitMqSettings _mqSettings;
    private readonly ILogger<UploadsService> _logger;

    public UploadsService(
        AppDbContext db,
        IFileStorageService storage,
        IQueueService queue,
        CsvJsonReviewParser parser,
        RabbitMqSettings mqSettings,
        ILogger<UploadsService> logger)
    {
        _db = db;
        _storage = storage;
        _queue = queue;
        _parser = parser;
        _mqSettings = mqSettings;
        _logger = logger;
    }

    public async Task<FileUploadDto> GetByIdAsync(Guid uploadId, CancellationToken ct)
    {
        var upload = await _db.FileUploads.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == uploadId, ct)
            ?? throw new NotFoundException($"Upload {uploadId} not found");

        return ToDto(upload);
    }

    public async Task<PaginatedResponse<FileUploadDto>> ListAsync(
        int? page, int? limit, string? status, string? sortBy, string? sortOrder, CancellationToken ct)
    {
        var (p, l) = PaginationParams.Normalize(page, limit);

        var query = _db.FileUploads.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = EnumParser.ParseFromMemberName<UploadStatus>(status)
                         ?? throw new ValidationException($"Invalid status '{status}'");
            query = query.Where(u => u.Status == parsed);
        }

        query = ApplySorting(query, sortBy, SortOrderParser.Parse(sortOrder));

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((p - 1) * l)
            .Take(l)
            .Select(MapToDto())
            .ToListAsync(ct);

        return PaginatedResponse<FileUploadDto>.Create(items, total, p, l);
    }

    private static IQueryable<FileUpload> ApplySorting(IQueryable<FileUpload> query, string? sortBy, SortOrder order)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : sortBy;
        var asc = order == SortOrder.Asc;

        return key switch
        {
            "fileName" => asc ? query.OrderBy(u => u.FileName) : query.OrderByDescending(u => u.FileName),
            "fileSize" => asc ? query.OrderBy(u => u.FileSize) : query.OrderByDescending(u => u.FileSize),
            "status" => asc ? query.OrderBy(u => u.Status) : query.OrderByDescending(u => u.Status),
            "totalRecords" => asc ? query.OrderBy(u => u.TotalRecords) : query.OrderByDescending(u => u.TotalRecords),
            _ => asc ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt)
        };
    }

    private static System.Linq.Expressions.Expression<Func<FileUpload, FileUploadDto>> MapToDto() =>
        u => new FileUploadDto
        {
            Id = u.Id,
            FileName = u.FileName,
            FileSize = u.FileSize,
            Status = u.Status,
            TotalRecords = u.TotalRecords,
            AnalyzedRecords = u.AnalyzedRecords,
            CreatedAt = u.CreatedAt,
            CompletedAt = u.CompletedAt,
            ErrorMessage = u.ErrorMessage
        };

    public async Task<FileUploadDto> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException("No file provided");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new PayloadTooLargeException("File exceeds 50 MB limit");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new UnsupportedMediaTypeException($"Unsupported file extension '{extension}'. Allowed: .csv, .json");
        }

        _logger.LogInformation(
            "Starting upload for file {FileName} ({Extension}, {FileSizeBytes} bytes)",
            file.FileName, extension, file.Length);

        var uploadId = Guid.NewGuid();

        string storageKey;
        await using (var uploadStream = file.OpenReadStream())
        {
            storageKey = await _storage.UploadFileAsync(uploadStream, file.FileName, file.ContentType ?? "application/octet-stream", ct);
        }

        var upload = new FileUpload
        {
            Id = uploadId,
            FileName = file.FileName,
            FileSize = file.Length,
            StorageKey = storageKey,
            Status = UploadStatus.Uploading,
            CreatedAt = DateTime.UtcNow
        };

        _db.FileUploads.Add(upload);
        await _db.SaveChangesAsync(ct);

        try
        {
            await using var storedStream = await _storage.DownloadFileAsync(storageKey, ct);
            var parsed = _parser.Parse(storedStream, file.FileName, uploadId);

            if (parsed.Reviews.Count == 0)
            {
                upload.Status = UploadStatus.Error;
                upload.ErrorMessage = "File contained no valid review records";
                upload.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                throw new UnprocessableEntityException("File is empty or contains no valid records");
            }

            _db.Reviews.AddRange(parsed.Reviews);
            upload.TotalRecords = parsed.Reviews.Count;
            upload.Status = UploadStatus.Analyzing;
            await _db.SaveChangesAsync(ct);

            await PublishAnalyzeBatchesAsync(upload, parsed.Reviews, ct);

            _logger.LogInformation(
                "Upload {UploadId} accepted. Records={TotalRecords}, Rejected={Rejected}",
                upload.Id, upload.TotalRecords, parsed.RejectedCount);
        }
        catch (UnprocessableEntityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process upload {UploadId}", upload.Id);
            upload.Status = UploadStatus.Error;
            upload.ErrorMessage = ex.Message;
            upload.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return ToDto(upload);
    }

    private async Task PublishAnalyzeBatchesAsync(FileUpload upload, List<Review> reviews, CancellationToken ct)
    {
        var batchSize = Math.Max(1, _mqSettings.BatchSize);
        var totalBatches = (int)Math.Ceiling((double)reviews.Count / batchSize);

        _logger.LogInformation(
            "Publishing {TotalReviews} reviews in {TotalBatches} batch(es) of {BatchSize} for upload {UploadId}",
            reviews.Count, totalBatches, batchSize, upload.Id);

        var batchIndex = 0;
        for (var i = 0; i < reviews.Count; i += batchSize)
        {
            batchIndex++;
            var batch = reviews.Skip(i).Take(batchSize)
                .Select(r => new ReviewInput
                {
                    Id = r.Id,
                    ClothingId = r.ClothingId,
                    Age = r.Age,
                    Title = r.Title,
                    ReviewText = r.ReviewText,
                    Rating = r.Rating,
                    RecommendedInd = r.RecommendedInd,
                    DivisionName = r.DivisionName,
                    DepartmentName = r.DepartmentName,
                    ClassName = r.ClassName
                })
                .ToList();

            _logger.LogDebug(
                "Publishing analyze batch {BatchIndex}/{TotalBatches} ({BatchCount} reviews) for upload {UploadId}",
                batchIndex, totalBatches, batch.Count, upload.Id);

            await _queue.PublishAnalyzeReviewsAsync(new AnalyzeReviewsMessage
            {
                TaskType = "analyze_reviews",
                UploadId = upload.Id,
                Reviews = batch
            }, ct);
        }
    }

    public async Task DeleteAsync(Guid uploadId, CancellationToken ct)
    {
        _logger.LogInformation("Deleting upload {UploadId}", uploadId);

        var upload = await _db.FileUploads.FirstOrDefaultAsync(u => u.Id == uploadId, ct)
                     ?? throw new NotFoundException($"Upload {uploadId} not found");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var deletedReviews = await _db.Reviews.Where(r => r.UploadId == uploadId).ExecuteDeleteAsync(ct);
        _db.FileUploads.Remove(upload);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Upload {UploadId} deleted from database ({DeletedReviews} reviews removed)",
            uploadId, deletedReviews);

        try
        {
            await _storage.DeleteFileAsync(upload.StorageKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete blob {StorageKey} for upload {UploadId} — file may remain in storage",
                upload.StorageKey, uploadId);
        }
    }

    private static FileUploadDto ToDto(FileUpload u) => new()
    {
        Id = u.Id,
        FileName = u.FileName,
        FileSize = u.FileSize,
        Status = u.Status,
        TotalRecords = u.TotalRecords,
        AnalyzedRecords = u.AnalyzedRecords,
        CreatedAt = u.CreatedAt,
        CompletedAt = u.CompletedAt,
        ErrorMessage = u.ErrorMessage
    };
}
