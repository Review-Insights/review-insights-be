using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Features.Reviews.Dtos;

namespace ReviewInsights.Api.Features.Reviews;

public class ReviewsService
{
    private readonly AppDbContext _db;

    public ReviewsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedResponse<ReviewListItemDto>> ListAsync(ReviewFilterParams filters, CancellationToken ct)
    {
        var (p, l) = PaginationParams.Normalize(filters.Page, filters.Limit);

        var query = ReviewQueryBuilder.Apply(_db.Reviews.AsNoTracking(), filters);

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

        return PaginatedResponse<ReviewListItemDto>.Create(items, total, p, l);
    }

    public async Task<ReviewDto> GetAsync(Guid id, CancellationToken ct)
    {
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
            ChurnProbability = r.ChurnProbability ?? 0,
            ChurnCauses = r.ChurnCauses,
            Priority = r.Priority,
            UploadId = r.UploadId,
            CreatedAt = r.CreatedAt,
            AnalyzedAt = r.AnalyzedAt
        };
    }
}
