using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Features.History;

public class HistoryService
{
    private static readonly int[] Windows = [7, 30, 60, 90, 180];
    private static readonly int MaxWindow = Windows.Max();

    private static readonly (AspectKey Value, string Key)[] AspectKeys =
        Enum.GetValues<AspectKey>().Select(a => (a, EnumParser.GetEnumMemberValue(a))).ToArray();
    private static readonly (ChurnCause Value, string Key)[] CauseKeys =
        Enum.GetValues<ChurnCause>().Select(c => (c, EnumParser.GetEnumMemberValue(c))).ToArray();

    private readonly AppDbContext _db;

    public HistoryService(AppDbContext db) => _db = db;

    public async Task<HistorySnapshotDto> GetSnapshotAsync(
        HistoryScopeParams scope, CancellationToken ct)
    {
        var snap = new HistorySnapshotDto();
        var now = DateTime.UtcNow;

        if (scope.ClothingId is int cid)
            snap.Product = await BuildSectionAsync(q => q.Where(r => r.ClothingId == cid), now, ct);

        if (!string.IsNullOrWhiteSpace(scope.ClassName))
        {
            var className = scope.ClassName;
            snap.Class = await BuildSectionAsync(q => q.Where(r => r.ClassName == className), now, ct);
        }

        if (!string.IsNullOrWhiteSpace(scope.DivisionName) && !string.IsNullOrWhiteSpace(scope.ClassName))
        {
            var div = scope.DivisionName;
            var cls = scope.ClassName;
            snap.Segment = await BuildSectionAsync(
                q => q.Where(r => r.DivisionName == div && r.ClassName == cls), now, ct);
        }

        return snap;
    }

    private async Task<HistorySectionDto> BuildSectionAsync(
        Func<IQueryable<Domain.Entities.Review>, IQueryable<Domain.Entities.Review>> filter,
        DateTime now, CancellationToken ct)
    {
        var since = now.AddDays(-MaxWindow);
        var rows = await filter(_db.Reviews.AsNoTracking())
            .Where(r => r.CreatedAt >= since)
            .Select(r => new ReviewRow(
                r.CreatedAt, r.OverallSentiment, r.RecommendedInd,
                r.PositiveFeedbackCount, r.AspectSentiments, r.ChurnCauses))
            .ToListAsync(ct);

        if (rows.Count == 0) return new HistorySectionDto();

        var section = new HistorySectionDto
        {
            AvgPositiveFeedback = rows.Average(r => (double)r.PositiveFeedbackCount),
        };

        foreach (var days in Windows)
        {
            var cutoff = now.AddDays(-days);
            var win = rows.Where(r => r.CreatedAt >= cutoff).ToList();
            if (win.Count == 0) continue;

            FillWindowBuckets(section, days, win);
        }

        return section;
    }

    private static void FillWindowBuckets(HistorySectionDto section, int days, IReadOnlyList<ReviewRow> win)
    {
        var total = win.Count;

        section.VeryNegativeCounts[days] = win.Count(r => r.OverallSentiment == Sentiment.VeryNegative);
        section.RecommendationRate[days] = (double)win.Count(r => r.RecommendedInd) / total;
        section.NegativeSentimentRate[days] = (double)win.Count(r =>
            r.OverallSentiment is Sentiment.Negative or Sentiment.VeryNegative) / total;

        foreach (var (aspect, key) in AspectKeys)
        {
            var n = win.Sum(r => r.AspectSentiments.Count(a =>
                a.Aspect == aspect &&
                (a.Sentiment is Sentiment.Negative or Sentiment.VeryNegative)));
            if (n > 0) GetOrAdd(section.AspectCounts, key)[days] = n;
        }

        foreach (var (cause, key) in CauseKeys)
        {
            var n = win.Sum(r => r.ChurnCauses.Count(c => c == cause));
            if (n > 0) GetOrAdd(section.CauseCounts, key)[days] = n;
        }
    }

    private static Dictionary<int, int> GetOrAdd(
        Dictionary<string, Dictionary<int, int>> outer, string key)
    {
        if (!outer.TryGetValue(key, out var inner))
        {
            inner = new Dictionary<int, int>();
            outer[key] = inner;
        }
        return inner;
    }

    private sealed record ReviewRow(
        DateTime CreatedAt,
        Sentiment? OverallSentiment,
        bool RecommendedInd,
        int PositiveFeedbackCount,
        List<AspectSentiment> AspectSentiments,
        List<ChurnCause> ChurnCauses);
}
