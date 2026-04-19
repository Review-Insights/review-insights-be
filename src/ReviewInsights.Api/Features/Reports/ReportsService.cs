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
                TotalRecords = r.TotalRecords
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

        var query = ApplyFilters(_db.Reviews.AsNoTracking(), payload.Filters);
        var totalRecords = await query.CountAsync(ct);
        if (totalRecords == 0)
        {
            throw new UnprocessableEntityException("No reviews match the provided filters");
        }

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

        try
        {
            var sample = await query
                .OrderBy(r => r.CreatedAt)
                .Take(_limits.MaxReviewsPerReport)
                .ToListAsync(ct);

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
                    ChurnProbability = r.ChurnProbability,
                    ChurnCauses = r.ChurnCauses,
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
            TotalRecords = report.TotalRecords
        };
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new NotFoundException($"Report {id} not found");

        _db.Reports.Remove(report);
        await _db.SaveChangesAsync(ct);
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
