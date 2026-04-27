using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;
using ReviewInsights.Api.Features.Products.Dtos;

namespace ReviewInsights.Api.Features.Products;

public class ProductsService
{
    private readonly AppDbContext _db;

    public ProductsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedResponse<ProductDto>> ListAsync(
        int? page, int? limit, string? search, string? departmentName, string? divisionName,
        string? sortBy, string? sortOrder, CancellationToken ct)
    {
        var (p, l) = PaginationParams.Normalize(page, limit);

        var filtered = BuildFilteredReviewsQuery(search, departmentName, divisionName);

        var aggregates = await BuildAggregatesQuery(filtered).ToListAsync(ct);
        var namesByProduct = await BuildNamesMapAsync(filtered, ct);

        foreach (var product in aggregates)
        {
            if (namesByProduct.TryGetValue(product.ClothingId, out var names))
            {
                product.ClassName = names.ClassName;
                product.DepartmentName = names.DepartmentName;
                product.DivisionName = names.DivisionName;
            }
        }

        var ordered = ApplySorting(aggregates, sortBy, SortOrderParser.Parse(sortOrder));

        var total = ordered.Count;
        var page1 = ordered.Skip((p - 1) * l).Take(l).ToList();

        return PaginatedResponse<ProductDto>.Create(page1, total, p, l);
    }

    private IQueryable<Domain.Entities.Review> BuildFilteredReviewsQuery(string? search, string? departmentName, string? divisionName)
    {
        var query = _db.Reviews.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(departmentName))
        {
            query = query.Where(r => r.DepartmentName == departmentName);
        }
        if (!string.IsNullOrWhiteSpace(divisionName))
        {
            query = query.Where(r => r.DivisionName == divisionName);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            var term = $"%{trimmed}%";
            query = query.Where(r =>
                r.ClothingId.ToString().Contains(trimmed) ||
                (r.ClassName != null && EF.Functions.ILike(r.ClassName, term)) ||
                (r.DepartmentName != null && EF.Functions.ILike(r.DepartmentName, term)));
        }

        return query;
    }

    private static IQueryable<ProductDto> BuildAggregatesQuery(IQueryable<Domain.Entities.Review> query)
    {
        return query
            .GroupBy(r => r.ClothingId)
            .Select(g => new ProductDto
            {
                ClothingId = g.Key,
                TotalReviews = g.Count(),
                AverageRating = g.Average(r => (double)r.Rating),
                RecommendationRate = (double)g.Count(r => r.RecommendedInd) / g.Count() * 100.0,
                AverageSentiment = g.Where(r => r.OverallSentiment != null)
                    .Average(r => (double?)(r.OverallSentiment == Sentiment.VeryPositive ? 1.0
                        : r.OverallSentiment == Sentiment.Positive ? 0.5
                        : r.OverallSentiment == Sentiment.Neutral ? 0.0
                        : r.OverallSentiment == Sentiment.Negative ? -0.5
                        : -1.0)) ?? 0.0,
                Priority = g.Max(r => r.Priority) ?? Priority.Low
            });
    }

    private static async Task<Dictionary<int, (string? ClassName, string? DepartmentName, string? DivisionName)>>
        BuildNamesMapAsync(IQueryable<Domain.Entities.Review> query, CancellationToken ct)
    {
        var raw = await query
            .GroupBy(r => new { r.ClothingId, r.ClassName, r.DepartmentName, r.DivisionName })
            .Select(g => new
            {
                g.Key.ClothingId,
                g.Key.ClassName,
                g.Key.DepartmentName,
                g.Key.DivisionName,
                Count = g.Count()
            })
            .ToListAsync(ct);

        return raw
            .GroupBy(x => x.ClothingId)
            .ToDictionary(
                g => g.Key,
                g => (
                    ClassName: PickModeByCount(g.Select(x => (x.ClassName, x.Count))),
                    DepartmentName: PickModeByCount(g.Select(x => (x.DepartmentName, x.Count))),
                    DivisionName: PickModeByCount(g.Select(x => (x.DivisionName, x.Count)))
                ));
    }

    private static string? PickModeByCount(IEnumerable<(string? Value, int Count)> pairs) =>
        pairs.Where(p => !string.IsNullOrEmpty(p.Value))
             .GroupBy(p => p.Value!)
             .Select(g => new { Value = g.Key, Total = g.Sum(x => x.Count) })
             .OrderByDescending(x => x.Total)
             .ThenBy(x => x.Value)
             .FirstOrDefault()?.Value;

    private static List<ProductDto> ApplySorting(List<ProductDto> items, string? sortBy, SortOrder order)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) ? "totalReviews" : sortBy.ToLowerInvariant();
        var asc = order == SortOrder.Asc;

        return key switch
        {
            "clothingid" => (asc ? items.OrderBy(x => x.ClothingId) : items.OrderByDescending(x => x.ClothingId)).ToList(),
            "averagerating" => (asc ? items.OrderBy(x => x.AverageRating) : items.OrderByDescending(x => x.AverageRating)).ToList(),
            "recommendationrate" => (asc ? items.OrderBy(x => x.RecommendationRate) : items.OrderByDescending(x => x.RecommendationRate)).ToList(),
            "averagesentiment" => (asc ? items.OrderBy(x => x.AverageSentiment) : items.OrderByDescending(x => x.AverageSentiment)).ToList(),
            "priority" => (asc ? items.OrderBy(x => x.Priority) : items.OrderByDescending(x => x.Priority)).ToList(),
            _ => (asc ? items.OrderBy(x => x.TotalReviews) : items.OrderByDescending(x => x.TotalReviews)).ToList()
        };
    }

    public async Task<ProductDetailDto> GetDetailAsync(int clothingId, CancellationToken ct)
    {
        var reviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.ClothingId == clothingId)
            .ToListAsync(ct);

        if (reviews.Count == 0)
        {
            throw new NotFoundException($"Product {clothingId} not found");
        }

        var detail = new ProductDetailDto
        {
            ClothingId = clothingId,
            ClassName = PickMostFrequent(reviews.Select(r => r.ClassName)),
            DepartmentName = PickMostFrequent(reviews.Select(r => r.DepartmentName)),
            DivisionName = PickMostFrequent(reviews.Select(r => r.DivisionName)),
            TotalReviews = reviews.Count,
            AverageRating = reviews.Average(r => (double)r.Rating),
            RecommendationRate = (double)reviews.Count(r => r.RecommendedInd) / reviews.Count * 100.0,
            AverageSentiment = reviews.Where(r => r.OverallSentiment != null)
                .Select(r => SentimentScore.ToScore(r.OverallSentiment))
                .DefaultIfEmpty(0.0)
                .Average(),
            Priority = reviews.Where(r => r.Priority != null)
                .Select(r => r.Priority!.Value)
                .DefaultIfEmpty(Priority.Low)
                .Max(),
            SentimentDistribution = BuildSentimentDistribution(reviews.Select(r => r.OverallSentiment)),
            RatingDistribution = BuildRatingDistribution(reviews.Select(r => r.Rating)),
            AspectSentiments = BuildAspectAggregates(reviews.SelectMany(r => r.AspectSentiments)),
            RecentTrends = BuildMonthlyTrends(reviews)
        };

        return detail;
    }

    public async Task<List<ProductTrendPointDto>> GetTrendsAsync(int clothingId, CancellationToken ct)
    {
        var exists = await _db.Reviews.AsNoTracking().AnyAsync(r => r.ClothingId == clothingId, ct);
        if (!exists) throw new NotFoundException($"Product {clothingId} not found");

        var reviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.ClothingId == clothingId)
            .ToListAsync(ct);

        return BuildMonthlyTrends(reviews);
    }

    public async Task<List<ProductAspectDataDto>> GetAspectsAsync(int clothingId, CancellationToken ct)
    {
        var exists = await _db.Reviews.AsNoTracking().AnyAsync(r => r.ClothingId == clothingId, ct);
        if (!exists) throw new NotFoundException($"Product {clothingId} not found");

        var reviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.ClothingId == clothingId)
            .ToListAsync(ct);

        return BuildAspectAggregates(reviews.SelectMany(r => r.AspectSentiments));
    }

    private static string? PickMostFrequent(IEnumerable<string?> values) =>
        values.Where(v => !string.IsNullOrEmpty(v))
              .GroupBy(v => v!)
              .OrderByDescending(g => g.Count())
              .ThenBy(g => g.Key)
              .FirstOrDefault()?.Key;

    private static Dictionary<string, int> BuildSentimentDistribution(IEnumerable<Sentiment?> sentiments)
    {
        var dict = new Dictionary<string, int>
        {
            [EnumParser.GetEnumMemberValue(Sentiment.VeryNegative)] = 0,
            [EnumParser.GetEnumMemberValue(Sentiment.Negative)] = 0,
            [EnumParser.GetEnumMemberValue(Sentiment.Neutral)] = 0,
            [EnumParser.GetEnumMemberValue(Sentiment.Positive)] = 0,
            [EnumParser.GetEnumMemberValue(Sentiment.VeryPositive)] = 0
        };
        foreach (var s in sentiments)
        {
            if (s is null) continue;
            var key = EnumParser.GetEnumMemberValue(s.Value);
            dict[key]++;
        }
        return dict;
    }

    private static Dictionary<int, int> BuildRatingDistribution(IEnumerable<int> ratings)
    {
        var dict = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0, [5] = 0 };
        foreach (var r in ratings)
        {
            if (r >= 1 && r <= 5) dict[r]++;
        }
        return dict;
    }

    private static List<ProductAspectDataDto> BuildAspectAggregates(IEnumerable<AspectSentiment> aspects)
    {
        return aspects
            .GroupBy(a => a.Aspect)
            .Select(g => new ProductAspectDataDto
            {
                Aspect = g.Key,
                AverageScore = g.Average(a => SentimentScore.ToScore(a.Sentiment)),
                Distribution = BuildSentimentDistribution(g.Select(a => (Sentiment?)a.Sentiment))
            })
            .OrderBy(x => x.Aspect.ToString())
            .ToList();
    }

    private static List<ProductTrendPointDto> BuildMonthlyTrends(IReadOnlyList<Domain.Entities.Review> reviews)
    {
        return reviews
            .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new ProductTrendPointDto
            {
                Period = $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                AverageRating = g.Average(r => (double)r.Rating),
                AverageSentiment = g.Where(r => r.OverallSentiment != null)
                    .Select(r => SentimentScore.ToScore(r.OverallSentiment))
                    .DefaultIfEmpty(0.0)
                    .Average(),
                ReviewCount = g.Count()
            })
            .ToList();
    }
}
