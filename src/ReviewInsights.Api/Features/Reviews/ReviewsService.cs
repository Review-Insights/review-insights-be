using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Features.Reviews.Dtos;

namespace ReviewInsights.Api.Features.Reviews;

public class ReviewsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReviewsService> _logger;

    public ReviewsService(AppDbContext db, ILogger<ReviewsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PaginatedResponse<ReviewListItemDto>> ListAsync(ReviewFilterParams filters, CancellationToken ct)
    {
        var (p, l) = PaginationParams.Normalize(filters.Page, filters.Limit);

        _logger.LogDebug(
            "Listing reviews: Page={Page}, Limit={Limit}, UploadId={UploadId}, Sentiment={Sentiment}",
            p, l, filters.UploadId, filters.Sentiment);

        IQueryable<Domain.Entities.Review> baseQuery;
        if (!string.IsNullOrWhiteSpace(filters.Aspect))
        {
            var parsedAspect = EnumParser.ParseFromMemberName<AspectKey>(filters.Aspect)
                               ?? throw new ValidationException($"Invalid aspect '{filters.Aspect}'");
            var jsonFilter = $"[{{\"Aspect\":{(int)parsedAspect}}}]";
            baseQuery = _db.Reviews
                .FromSqlInterpolated($"SELECT * FROM reviews WHERE aspect_sentiments @> {jsonFilter}::jsonb")
                .AsNoTracking();
        }
        else
        {
            baseQuery = _db.Reviews.AsNoTracking();
        }

        var query = ReviewQueryBuilder.Apply(baseQuery, filters);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((p - 1) * l)
            .Take(l)
            .Select(r => new ReviewListItemDto
            {
                Id = r.Id,
                ClothingId = r.ClothingId,
                Age = r.Age,
                Title = r.Title,
                ReviewText = r.ReviewText,
                Rating = r.Rating,
                RecommendedInd = r.RecommendedInd,
                OverallSentiment = r.OverallSentiment,
                Priority = r.Priority,
                DepartmentName = r.DepartmentName,
                ClassName = r.ClassName,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);

        _logger.LogDebug(
            "Listed reviews: Total={Total}, Returned={Returned}, Page={Page}",
            total, items.Count, p);

        return PaginatedResponse<ReviewListItemDto>.Create(items, total, p, l);
    }

    public async Task<ReviewDto> GetAsync(Guid id, CancellationToken ct)
    {
        _logger.LogDebug("Fetching review {ReviewId}", id);

        var r = await _db.Reviews.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"Review {id} not found");

        return new ReviewDto
        {
            Id = r.Id,
            ClothingId = r.ClothingId,
            Age = r.Age,
            Title = r.Title,
            ReviewText = r.ReviewText,
            Rating = r.Rating,
            RecommendedInd = r.RecommendedInd,
            PositiveFeedbackCount = r.PositiveFeedbackCount,
            DivisionName = r.DivisionName,
            DepartmentName = r.DepartmentName,
            ClassName = r.ClassName,
            OverallSentiment = r.OverallSentiment,
            AspectSentiments = r.AspectSentiments,
            Priority = r.Priority,
            UploadId = r.UploadId,
            CreatedAt = r.CreatedAt,
            AnalyzedAt = r.AnalyzedAt
        };
    }
}
