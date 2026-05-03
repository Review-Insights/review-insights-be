using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Features.Dashboard.Dtos;

namespace ReviewInsights.Api.Features.Dashboard;

public class DashboardService
{
    private static readonly CultureInfo PlCulture = CultureInfo.GetCultureInfo("pl-PL");
    private readonly AppDbContext _db;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(AppDbContext db, ILogger<DashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<DashboardStatCardDto>> GetStatsAsync(CancellationToken ct)
    {
        _logger.LogDebug("Fetching dashboard stats");
        var now = DateTime.UtcNow;
        var oneWeekAgo = now.AddDays(-7);
        var twoWeeksAgo = now.AddDays(-14);
        var oneMonthAgo = now.AddMonths(-1);
        var twoMonthsAgo = now.AddMonths(-2);

        var totalReviews = await _db.Reviews.CountAsync(ct);
        var newThisWeek = await _db.Reviews.CountAsync(r => r.CreatedAt >= oneWeekAgo, ct);
        var newPrevWeek = await _db.Reviews.CountAsync(r => r.CreatedAt >= twoWeeksAgo && r.CreatedAt < oneWeekAgo, ct);

        var avgRating = totalReviews > 0
            ? await _db.Reviews.AverageAsync(r => (double)r.Rating, ct)
            : 0.0;
        var avgRatingPrev = await _db.Reviews
            .Where(r => r.CreatedAt < oneMonthAgo && r.CreatedAt >= twoMonthsAgo)
            .Select(r => (double?)r.Rating)
            .AverageAsync(ct) ?? 0.0;

        var recommendationRate = totalReviews > 0
            ? await _db.Reviews.CountAsync(r => r.RecommendedInd, ct) / (double)totalReviews * 100.0
            : 0.0;

        var prevMonthCount = await _db.Reviews.CountAsync(r => r.CreatedAt < oneMonthAgo && r.CreatedAt >= twoMonthsAgo, ct);
        var recommendationRatePrev = prevMonthCount > 0
            ? await _db.Reviews.CountAsync(r => r.CreatedAt < oneMonthAgo && r.CreatedAt >= twoMonthsAgo && r.RecommendedInd, ct)
              / (double)prevMonthCount * 100.0
            : 0.0;

        var highPriority = await _db.Reviews.CountAsync(
            r => r.Priority == Priority.High || r.Priority == Priority.Critical, ct);
        var highPriorityThisWeek = await _db.Reviews.CountAsync(
            r => (r.Priority == Priority.High || r.Priority == Priority.Critical) && r.CreatedAt >= oneWeekAgo, ct);

        return
        [
            BuildCard("total_reviews",
                totalReviews.ToString("N0", PlCulture),
                FormatChange(newThisWeek - newPrevWeek),
                Trend(newThisWeek - newPrevWeek),
                "week"),
            BuildCard("average_rating",
                $"{avgRating.ToString("F1", PlCulture)} / 5",
                FormatChangeDouble(avgRating - avgRatingPrev),
                Trend(avgRating - avgRatingPrev),
                "month"),
            BuildCard("recommendation_rate",
                $"{recommendationRate.ToString("F1", PlCulture)}%",
                FormatChangeDouble(recommendationRate - recommendationRatePrev, "%"),
                Trend(recommendationRate - recommendationRatePrev),
                "month"),
            BuildCard("high_priority",
                highPriority.ToString("N0", PlCulture),
                FormatChange(highPriorityThisWeek),
                Trend(highPriorityThisWeek),
                "week")
        ];
    }

    private static DashboardStatCardDto BuildCard(string key, string value, string change, string trend, string? period = null) => new()
    {
        Key = key,
        Value = value,
        Change = change,
        Trend = trend,
        Icon = key,
        Period = period
    };

    private static string Trend(double delta) => delta switch
    {
        > 0.0001 => "up",
        < -0.0001 => "down",
        _ => "neutral"
    };

    private static string FormatChange(int delta)
    {
        var sign = delta >= 0 ? "+" : "";
        return $"{sign}{delta.ToString("N0", PlCulture)}";
    }

    private static string FormatChangeDouble(double delta, string? suffixUnit = null)
    {
        var sign = delta >= 0 ? "+" : "";
        var unit = suffixUnit ?? string.Empty;
        return $"{sign}{delta.ToString("F1", PlCulture)}{unit}";
    }

    public async Task<List<SentimentTrendPointDto>> GetSentimentTrendAsync(string? period, CancellationToken ct)
    {
        _logger.LogDebug("Fetching sentiment trend for period {Period}", period ?? "30d");

        var (from, granularity) = ResolvePeriod(period);

        var query = _db.Reviews.AsNoTracking()
            .Where(r => r.OverallSentiment != null);
        if (from is not null) query = query.Where(r => r.CreatedAt >= from.Value);

        var reviews = await query
            .Select(r => new { r.CreatedAt, r.OverallSentiment })
            .ToListAsync(ct);

        var grouped = reviews
            .GroupBy(r => GroupKey(r.CreatedAt, granularity))
            .OrderBy(g => g.Key)
            .Select(g => new SentimentTrendPointDto
            {
                Period = g.Key,
                Positive = g.Count(r => r.OverallSentiment is Sentiment.Positive or Sentiment.VeryPositive),
                Neutral = g.Count(r => r.OverallSentiment is Sentiment.Neutral),
                Negative = g.Count(r => r.OverallSentiment is Sentiment.Negative or Sentiment.VeryNegative)
            })
            .ToList();

        return grouped;
    }

    private enum Granularity { Day, Week, Month }

    private static (DateTime? From, Granularity G) ResolvePeriod(string? period)
    {
        var now = DateTime.UtcNow;
        return period switch
        {
            "7d" => (now.AddDays(-7), Granularity.Day),
            "30d" => (now.AddDays(-30), Granularity.Day),
            "90d" => (now.AddDays(-90), Granularity.Week),
            "all" => (null, Granularity.Month),
            null => (now.AddDays(-30), Granularity.Day),
            _ => throw new ValidationException($"Invalid period '{period}'. Allowed: 7d, 30d, 90d, all")
        };
    }

    private static string GroupKey(DateTime date, Granularity g) => g switch
    {
        Granularity.Day => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Granularity.Week => $"{ISOWeek.GetYear(date):D4}-W{ISOWeek.GetWeekOfYear(date):D2}",
        Granularity.Month => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    public async Task<List<RatingDistributionItemDto>> GetRatingDistributionAsync(CancellationToken ct)
    {
        var counts = await _db.Reviews
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = counts.Sum(c => c.Count);
        var result = new List<RatingDistributionItemDto>();
        for (var rating = 1; rating <= 5; rating++)
        {
            var count = counts.FirstOrDefault(c => c.Rating == rating)?.Count ?? 0;
            result.Add(new RatingDistributionItemDto
            {
                Rating = rating,
                Count = count,
                Percentage = total > 0 ? Math.Round(count / (double)total * 100.0, 1) : 0.0
            });
        }
        return result;
    }

    public async Task<List<DepartmentStatItemDto>> GetDepartmentStatsAsync(CancellationToken ct)
    {
        var grouped = await _db.Reviews.AsNoTracking()
            .Where(r => r.DepartmentName != null)
            .GroupBy(r => r.DepartmentName!)
            .Select(g => new
            {
                Department = g.Key,
                TotalReviews = g.Count(),
                AverageRating = g.Average(r => (double)r.Rating),
                Recommended = g.Count(r => r.RecommendedInd),
                AverageSentiment = g.Where(r => r.OverallSentiment != null)
                    .Average(r => (double?)(r.OverallSentiment == Sentiment.VeryPositive ? 1.0
                        : r.OverallSentiment == Sentiment.Positive ? 0.5
                        : r.OverallSentiment == Sentiment.Neutral ? 0.0
                        : r.OverallSentiment == Sentiment.Negative ? -0.5
                        : -1.0)) ?? 0.0
            })
            .ToListAsync(ct);

        return grouped
            .Where(g => g.TotalReviews > 0)
            .Select(g => new DepartmentStatItemDto
            {
                Department = g.Department,
                AverageRating = Math.Round(g.AverageRating, 2),
                TotalReviews = g.TotalReviews,
                RecommendationRate = Math.Round(g.Recommended / (double)g.TotalReviews * 100.0, 1),
                AverageSentiment = Math.Round(g.AverageSentiment, 2)
            })
            .OrderByDescending(d => d.TotalReviews)
            .ToList();
    }
}
