using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;
using ReviewInsights.Api.Features.Products.Dtos;

namespace ReviewInsights.Api.Features.Products;

public class ProductsService
{
    private const double PriorityHalfLifeDays = 90;
    private const double CriticalWilsonThreshold = 0.20;
    private const double HighWilsonThreshold = 0.30;
    private const double MediumShareThreshold = 0.40;
    private const double ClassOutlierMultiplier = 1.5;
    private const double ClassOutlierMinShare = 0.10;
    private const int StaleCutoffDays = 365;

    private readonly AppDbContext _db;
    private readonly ILogger<ProductsService> _logger;

    public ProductsService(AppDbContext db, ILogger<ProductsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PaginatedResponse<ProductDto>> ListAsync(
        int? page, int? limit, string? search, string? departmentName, string? divisionName,
        string? sortBy, string? sortOrder, CancellationToken ct)
    {
        var (p, l) = PaginationParams.Normalize(page, limit);

        _logger.LogDebug(
            "Listing products: Page={Page}, Limit={Limit}, Search={Search}, Department={Department}, Division={Division}",
            p, l, search, departmentName, divisionName);

        var filtered = BuildFilteredReviewsQuery(search, departmentName, divisionName);

        var aggregates = await BuildAggregatesQuery(filtered).ToListAsync(ct);
        var namesByProduct = await BuildNamesMapAsync(filtered, ct);
        var priorityInputs = await BuildPriorityInputsQuery(filtered).ToListAsync(ct);

        foreach (var product in aggregates)
        {
            if (namesByProduct.TryGetValue(product.ClothingId, out var names))
            {
                product.ClassName = names.ClassName;
                product.DepartmentName = names.DepartmentName;
                product.DivisionName = names.DivisionName;
            }
        }

        var classBaselines = ComputeClassCriticalRates(priorityInputs);
        var reviewsByProduct = priorityInputs
            .GroupBy(r => r.ClothingId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProductPriorityInput>)g.ToList());

        foreach (var product in aggregates)
        {
            if (!reviewsByProduct.TryGetValue(product.ClothingId, out var reviews))
            {
                product.Priority = Priority.Low;
                continue;
            }
            var classRate = product.ClassName is not null
                && classBaselines.TryGetValue(product.ClassName, out var rate)
                    ? (double?)rate
                    : null;
            var verdict = ComputePriority(reviews, classRate);
            product.Priority = verdict.Priority;
            product.PriorityRule = verdict.RuleId;
            product.PriorityReason = verdict.Reason;
        }

        var ordered = ApplySorting(aggregates, sortBy, SortOrderParser.Parse(sortOrder));

        var total = ordered.Count;
        var page1 = ordered.Skip((p - 1) * l).Take(l).ToList();

        _logger.LogDebug("Listed products: Total={Total}, Returned={Returned}", total, page1.Count);

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
                        : -1.0)) ?? 0.0
            });
    }

    private static IQueryable<ProductPriorityInput> BuildPriorityInputsQuery(IQueryable<Domain.Entities.Review> query) =>
        query.Select(r => new ProductPriorityInput
        {
            ClothingId = r.ClothingId,
            CreatedAt = r.CreatedAt,
            Priority = r.Priority,
            PriorityRule = r.PriorityRule,
            ClassName = r.ClassName,
        });

    private static ProductPriorityInput ToPriorityInput(Domain.Entities.Review r) => new()
    {
        ClothingId = r.ClothingId,
        CreatedAt = r.CreatedAt,
        Priority = r.Priority,
        PriorityRule = r.PriorityRule,
        ClassName = r.ClassName,
    };

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
        _logger.LogDebug("Fetching product detail for ClothingId={ClothingId}", clothingId);

        var reviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.ClothingId == clothingId)
            .ToListAsync(ct);

        if (reviews.Count == 0)
        {
            throw new NotFoundException($"Product {clothingId} not found");
        }

        var className = PickMostFrequent(reviews.Select(r => r.ClassName));
        var classRate = await ComputeClassCriticalRateAsync(className, ct);
        var verdict = ComputePriority(reviews.Select(ToPriorityInput).ToList(), classRate);

        var detail = new ProductDetailDto
        {
            ClothingId = clothingId,
            ClassName = className,
            DepartmentName = PickMostFrequent(reviews.Select(r => r.DepartmentName)),
            DivisionName = PickMostFrequent(reviews.Select(r => r.DivisionName)),
            TotalReviews = reviews.Count,
            AverageRating = reviews.Average(r => (double)r.Rating),
            RecommendationRate = (double)reviews.Count(r => r.RecommendedInd) / reviews.Count * 100.0,
            AverageSentiment = reviews.Where(r => r.OverallSentiment != null)
                .Select(r => SentimentScore.ToScore(r.OverallSentiment))
                .DefaultIfEmpty(0.0)
                .Average(),
            Priority = verdict.Priority,
            PriorityRule = verdict.RuleId,
            PriorityReason = verdict.Reason,
            SentimentDistribution = BuildSentimentDistribution(reviews.Select(r => r.OverallSentiment)),
            RatingDistribution = BuildRatingDistribution(reviews.Select(r => r.Rating)),
            AspectSentiments = BuildAspectAggregates(reviews.SelectMany(r => r.AspectSentiments)),
            RecentTrends = BuildMonthlyTrends(reviews)
        };

        return detail;
    }

    private async Task<double?> ComputeClassCriticalRateAsync(
        string? className, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(className)) return null;
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var rows = await _db.Reviews.AsNoTracking()
            .Where(r => r.ClassName == className && r.CreatedAt >= cutoff)
            .Select(r => r.Priority)
            .ToListAsync(ct);
        if (rows.Count == 0) return null;
        return (double)rows.Count(p => p == Priority.Critical) / rows.Count;
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

    private static ProductPriorityVerdict ComputePriority(
        IReadOnlyList<ProductPriorityInput> reviews,
        double? classCriticalRate)
    {
        if (reviews.Count == 0)
            return new(Priority.Low, null, "No reviews");

        var now = DateTime.UtcNow;
        var newestAgeDays = (int)(now - reviews.Max(r => r.CreatedAt)).TotalDays;
        if (newestAgeDays > StaleCutoffDays)
            return new(Priority.Low, null,
                $"Stale: newest review {newestAgeDays} days old (cutoff {StaleCutoffDays})");

        var weighted = AccumulateWeighted(reviews, now);
        if (weighted.Total <= 0)
            return new(Priority.Low, null, "All reviews fully decayed");

        return ClassifyPriority(weighted, classCriticalRate);
    }

    private static WeightedAccumulator AccumulateWeighted(
        IReadOnlyList<ProductPriorityInput> reviews, DateTime now)
    {
        var acc = new WeightedAccumulator();
        var decayRate = Math.Log(2) / PriorityHalfLifeDays;
        foreach (var r in reviews)
        {
            var ageDays = Math.Max(0, (now - r.CreatedAt).TotalDays);
            var w = Math.Exp(-decayRate * ageDays);
            acc.Total += w;
            switch (r.Priority)
            {
                case Priority.Critical: acc.Critical += w; break;
                case Priority.High:     acc.High += w; break;
                case Priority.Medium:   acc.Medium += w; break;
            }
            if (!string.IsNullOrEmpty(r.PriorityRule))
            {
                acc.RuleWeights[r.PriorityRule] =
                    acc.RuleWeights.GetValueOrDefault(r.PriorityRule) + w;
            }
        }
        return acc;
    }

    private static ProductPriorityVerdict ClassifyPriority(
        WeightedAccumulator w, double? classCriticalRate)
    {
        var critShare = w.Critical / w.Total;
        var wilsonCrit = WilsonLowerBound(w.Critical, w.Total);
        var wilsonHigh = WilsonLowerBound(w.Critical + w.High, w.Total);
        var combinedShare = (w.Critical + w.High + w.Medium) / w.Total;
        var ruleId = w.DominantRule;

        if (wilsonCrit >= CriticalWilsonThreshold)
            return new(Priority.Critical, ruleId,
                $"Recency-weighted critical share {critShare:P0} (Wilson LB {wilsonCrit:P0}) ≥ {CriticalWilsonThreshold:P0}");

        if (classCriticalRate is double baseline
            && critShare >= ClassOutlierMinShare
            && critShare >= baseline * ClassOutlierMultiplier)
            return new(Priority.Critical, ruleId,
                $"Critical share {critShare:P0} ≥ {ClassOutlierMultiplier}× class baseline ({baseline:P0})");

        if (wilsonHigh >= HighWilsonThreshold)
            return new(Priority.High, ruleId,
                $"Critical+high Wilson LB {wilsonHigh:P0} ≥ {HighWilsonThreshold:P0}");

        if (combinedShare >= MediumShareThreshold)
            return new(Priority.Medium, ruleId,
                $"Combined non-low share {combinedShare:P0} ≥ {MediumShareThreshold:P0}");

        return new(Priority.Low, null,
            $"Issue rates below thresholds (critical {critShare:P0})");
    }

    private static double WilsonLowerBound(double successesW, double totalW)
    {
        if (totalW <= 0) return 0;
        const double z = 1.96;
        var p = successesW / totalW;
        var denom = 1 + z * z / totalW;
        var center = p + z * z / (2 * totalW);
        var margin = z * Math.Sqrt(p * (1 - p) / totalW + z * z / (4 * totalW * totalW));
        return Math.Max(0, (center - margin) / denom);
    }

    private static IReadOnlyDictionary<string, double> ComputeClassCriticalRates(
        IEnumerable<ProductPriorityInput> reviews)
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);
        return reviews
            .Where(r => !string.IsNullOrEmpty(r.ClassName) && r.CreatedAt >= cutoff)
            .GroupBy(r => r.ClassName!)
            .ToDictionary(
                g => g.Key,
                g => (double)g.Count(r => r.Priority == Priority.Critical) / g.Count());
    }

    private sealed record ProductPriorityVerdict(Priority Priority, string? RuleId, string? Reason);

    private sealed class WeightedAccumulator
    {
        public double Total { get; set; }
        public double Critical { get; set; }
        public double High { get; set; }
        public double Medium { get; set; }
        public Dictionary<string, double> RuleWeights { get; } = new();

        public string? DominantRule => RuleWeights.Count == 0
            ? null
            : RuleWeights.OrderByDescending(kv => kv.Value).First().Key;
    }

    private sealed class ProductPriorityInput
    {
        public int ClothingId { get; init; }
        public DateTime CreatedAt { get; init; }
        public Priority? Priority { get; init; }
        public string? PriorityRule { get; init; }
        public string? ClassName { get; init; }
    }
}
