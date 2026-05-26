using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Configuration;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Entities;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Features.Reports.Dtos;
using ReviewInsights.Api.Infrastructure;
using ReviewInsights.Api.Messaging;

namespace ReviewInsights.Api.Features.Reports;

public class ReportsService
{
    private readonly AppDbContext _db;
    private readonly IQueueService _queue;
    private readonly ReportLimits _limits;
    private readonly ILogger<ReportsService> _logger;

    public ReportsService(AppDbContext db, IQueueService queue, ReportLimits limits, ILogger<ReportsService> logger)
    {
        _db = db;
        _queue = queue;
        _limits = limits;
        _logger = logger;
    }

    public async Task<PaginatedResponse<ReportListItemDto>> ListAsync(int? page, int? limit, CancellationToken ct)
    {
        var (p, l) = PaginationParams.Normalize(page, limit);

        var query = _db.Reports.AsNoTracking().OrderByDescending(r => r.GeneratedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((p - 1) * l)
            .Take(l)
            .Select(r => new ReportListItemDto
            {
                Id = r.Id,
                Title = r.Title,
                Status = r.Status,
                Filters = r.Filters,
                GeneratedAt = r.GeneratedAt,
                CompletedAt = r.CompletedAt,
                TotalRecords = r.TotalRecords,
                ErrorMessage = r.ErrorMessage
            })
            .ToListAsync(ct);

        return PaginatedResponse<ReportListItemDto>.Create(items, total, p, l);
    }

    public async Task<ReportDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.Reports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"Report {id} not found");

        return new ReportDetailDto
        {
            Id = r.Id,
            Title = r.Title,
            Status = r.Status,
            Filters = r.Filters,
            GeneratedAt = r.GeneratedAt,
            CompletedAt = r.CompletedAt,
            TotalRecords = r.TotalRecords,
            ErrorMessage = r.ErrorMessage,
            Scope = r.Scope,
            Summary = r.Summary,
            Insights = r.Insights,
            Suggestions = r.Suggestions
        };
    }

    public async Task<Report> GetEntityAsync(Guid id, CancellationToken ct)
    {
        return await _db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
               ?? throw new NotFoundException($"Report {id} not found");
    }

    public async Task<ReportListItemDto> GenerateAsync(GenerateReportPayload payload, CancellationToken ct)
    {
        ValidatePayload(payload);

        _logger.LogInformation(
            "Generating report '{Title}' with filters: Department={Department}, Division={Division}, ClothingId={ClothingId}",
            payload.Title.Trim(),
            payload.Filters.DepartmentName,
            payload.Filters.DivisionName,
            payload.Filters.ClothingId);

        var baseQuery = ApplyFilters(_db.Reviews.AsNoTracking(), payload.Filters);
        var preview = await BuildGeneratePreviewAsync(baseQuery, ct);
        EnsureGenerationAllowed(preview);
        var totalRecords = preview.ProcessedRecords;
        var query = ApplyProcessedReviewsFilter(baseQuery);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            Title = payload.Title.Trim(),
            Status = ReportStatus.Generating,
            Filters = payload.Filters,
            GeneratedAt = DateTime.UtcNow,
            TotalRecords = totalRecords
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Report {ReportId} '{Title}' created with {TotalRecords} processed reviews. Skipped {SkippedRecords} not processed reviews",
            report.Id, report.Title, totalRecords, preview.SkippedRecords);

        try
        {
            var sample = await query
                .OrderBy(r => r.CreatedAt)
                .Take(_limits.MaxReviewsPerReport)
                .ToListAsync(ct);

            _logger.LogInformation(
                "Dispatching report {ReportId} to AI worker with {ReviewCount} processed reviews (limit={Limit}, skipped={SkippedRecords})",
                report.Id, sample.Count, _limits.MaxReviewsPerReport, preview.SkippedRecords);

            var message = new GenerateReportMessage
            {
                TaskType = "generate_report",
                ReportId = report.Id,
                Filters = payload.Filters,
                Reviews = sample.Select(r => new AnalyzedReviewPayload
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
                    ClassName = r.ClassName,
                    OverallSentiment = r.OverallSentiment,
                    AspectSentiments = r.AspectSentiments,
                    Priority = r.Priority,
                    CreatedAt = r.CreatedAt,
                    AnalyzedAt = r.AnalyzedAt
                }).ToList()
            };

            await _queue.PublishGenerateReportAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch report {ReportId} to AI worker", report.Id);
            report.Status = ReportStatus.Failed;
            report.ErrorMessage = ex.Message;
            report.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new ReportListItemDto
        {
            Id = report.Id,
            Title = report.Title,
            Status = report.Status,
            Filters = report.Filters,
            GeneratedAt = report.GeneratedAt,
            CompletedAt = report.CompletedAt,
            TotalRecords = report.TotalRecords,
            ErrorMessage = report.ErrorMessage
        };
    }

    public async Task<GenerateReportPreviewDto> PreviewGenerateAsync(GenerateReportPayload payload, CancellationToken ct)
    {
        ValidatePayload(payload);
        var baseQuery = ApplyFilters(_db.Reviews.AsNoTracking(), payload.Filters);
        return await BuildGeneratePreviewAsync(baseQuery, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting report {ReportId}", id);

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new NotFoundException($"Report {id} not found");

        _db.Reports.Remove(report);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Report {ReportId} '{Title}' deleted", id, report.Title);
    }

    private static void ValidatePayload(GenerateReportPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Title) || payload.Title.Trim().Length < 3)
        {
            throw new ValidationException("Title must be at least 3 characters");
        }

        var f = payload.Filters;
        if (f.MinRating is not null && (f.MinRating < 1 || f.MinRating > 5))
        {
            throw new ValidationException("minRating must be between 1 and 5");
        }
        if (f.MaxRating is not null && (f.MaxRating < 1 || f.MaxRating > 5))
        {
            throw new ValidationException("maxRating must be between 1 and 5");
        }
        if (f.MinRating is not null && f.MaxRating is not null && f.MaxRating < f.MinRating)
        {
            throw new ValidationException("maxRating must be greater than or equal to minRating");
        }
        if (f.DateFrom is not null && f.DateTo is not null && f.DateTo < f.DateFrom)
        {
            throw new ValidationException("dateTo must be greater than or equal to dateFrom");
        }
        if (!string.IsNullOrWhiteSpace(f.DepartmentName) && !DomainConstants.Departments.Contains(f.DepartmentName))
        {
            throw new ValidationException($"Invalid departmentName '{f.DepartmentName}'");
        }
        if (!string.IsNullOrWhiteSpace(f.DivisionName) && !DomainConstants.Divisions.Contains(f.DivisionName))
        {
            throw new ValidationException($"Invalid divisionName '{f.DivisionName}'");
        }
        if (!string.IsNullOrWhiteSpace(f.ClassName) && !DomainConstants.ClassNames.Contains(f.ClassName))
        {
            throw new ValidationException($"Invalid className '{f.ClassName}'");
        }
        if (f.ClothingId is not null && f.ClothingId <= 0)
        {
            throw new ValidationException("clothingId must be a positive integer");
        }
    }

    private async Task<GenerateReportPreviewDto> BuildGeneratePreviewAsync(IQueryable<Review> baseQuery, CancellationToken ct)
    {
        var totalMatchingRecords = await baseQuery.CountAsync(ct);
        var processedRecords = await ApplyProcessedReviewsFilter(baseQuery).CountAsync(ct);
        var skippedRecords = totalMatchingRecords - processedRecords;

        return new GenerateReportPreviewDto
        {
            TotalMatchingRecords = totalMatchingRecords,
            ProcessedRecords = processedRecords,
            SkippedRecords = skippedRecords,
            MaxRecordsLimit = _limits.MaxReviewsPerReport,
            CanGenerate = totalMatchingRecords > 0
                && processedRecords > 0
                && processedRecords <= _limits.MaxReviewsPerReport,
            Message = BuildPreviewMessage(totalMatchingRecords, processedRecords, skippedRecords)
        };
    }

    private void EnsureGenerationAllowed(GenerateReportPreviewDto preview)
    {
        if (preview.TotalMatchingRecords == 0)
        {
            _logger.LogWarning("Report generation aborted: no reviews match the provided filters");
            throw new UnprocessableEntityException("No reviews match the provided filters");
        }

        if (preview.ProcessedRecords == 0)
        {
            _logger.LogWarning("Report generation aborted: matching reviews are not processed yet");
            throw new UnprocessableEntityException(
                "Reviews matching the filters have not been analyzed yet. Please wait for analysis to complete and try again."
            );
        }

        if (preview.ProcessedRecords > _limits.MaxReviewsPerReport)
        {
            _logger.LogWarning(
                "Report generation aborted: processed review count {ProcessedRecords} exceeds limit {Limit}",
                preview.ProcessedRecords, _limits.MaxReviewsPerReport);
            throw new ValidationException(
                $"Report cannot be generated for more than {_limits.MaxReviewsPerReport} processed records."
            );
        }
    }

    private string? BuildPreviewMessage(int totalMatchingRecords, int processedRecords, int skippedRecords)
    {
        if (totalMatchingRecords == 0)
        {
            return "No reviews match the provided filters.";
        }

        if (processedRecords == 0)
        {
            return "All matching records are skipped because they're not processed yet.";
        }

        if (processedRecords > _limits.MaxReviewsPerReport)
        {
            return $"Report cannot be generated for more than {_limits.MaxReviewsPerReport} processed records.";
        }

        if (skippedRecords > 0)
        {
            return $"Skipping {skippedRecords} records because they're not processed.";
        }

        return null;
    }

    private static IQueryable<Review> ApplyProcessedReviewsFilter(IQueryable<Review> query)
    {
        return query.Where(r =>
            r.AnalyzedAt != null
            && r.OverallSentiment != null
            && r.Priority != null);
    }

    public static IQueryable<Review> ApplyFilters(IQueryable<Review> query, Domain.ValueObjects.ReportFilters f)
    {
        if (f.DateFrom is not null)
        {
            var from = DateTime.SpecifyKind(f.DateFrom.Value, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt >= from);
        }
        if (f.DateTo is not null)
        {
            var to = DateTime.SpecifyKind(f.DateTo.Value, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt <= to);
        }
        if (!string.IsNullOrWhiteSpace(f.DepartmentName)) query = query.Where(r => r.DepartmentName == f.DepartmentName);
        if (!string.IsNullOrWhiteSpace(f.DivisionName)) query = query.Where(r => r.DivisionName == f.DivisionName);
        if (!string.IsNullOrWhiteSpace(f.ClassName)) query = query.Where(r => r.ClassName == f.ClassName);
        if (f.ClothingId is not null) query = query.Where(r => r.ClothingId == f.ClothingId.Value);
        if (f.MinRating is not null) query = query.Where(r => r.Rating >= f.MinRating.Value);
        if (f.MaxRating is not null) query = query.Where(r => r.Rating <= f.MaxRating.Value);
        return query;
    }
}
