using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Domain.Entities;
using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Features.Reviews;

public class ReviewFilterParams
{
    public int? Page { get; set; }
    public int? Limit { get; set; }
    public string? Search { get; set; }
    public int? Rating { get; set; }
    public string? Sentiment { get; set; }
    public string? Priority { get; set; }
    public string? DepartmentName { get; set; }
    public string? DivisionName { get; set; }
    public string? ClassName { get; set; }
    public bool? Recommended { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public Guid? UploadId { get; set; }
    public int? ClothingId { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
}

public static class ReviewQueryBuilder
{
    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "createdAt", "rating", "age", "overallSentiment", "priority"
        };

    public static IQueryable<Review> Apply(IQueryable<Review> query, ReviewFilterParams filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = $"%{filters.Search.Trim()}%";
            query = query.Where(r =>
                (r.Title != null && EF.Functions.ILike(r.Title, term)) ||
                (r.ReviewText != null && EF.Functions.ILike(r.ReviewText, term)));
        }

        if (filters.Rating is not null)
        {
            query = query.Where(r => r.Rating == filters.Rating.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Sentiment))
        {
            var parsed = EnumParser.ParseFromMemberName<Sentiment>(filters.Sentiment)
                         ?? throw new ValidationException($"Invalid sentiment '{filters.Sentiment}'");
            query = query.Where(r => r.OverallSentiment == parsed);
        }

        if (!string.IsNullOrWhiteSpace(filters.Priority))
        {
            var parsed = EnumParser.ParseListFromMemberName<Priority>(filters.Priority);
            if (parsed.Length > 0)
            {
                query = query.Where(r => r.Priority != null && parsed.Contains(r.Priority.Value));
            }
        }

        if (!string.IsNullOrWhiteSpace(filters.DepartmentName))
        {
            query = query.Where(r => r.DepartmentName == filters.DepartmentName);
        }
        if (!string.IsNullOrWhiteSpace(filters.DivisionName))
        {
            query = query.Where(r => r.DivisionName == filters.DivisionName);
        }
        if (!string.IsNullOrWhiteSpace(filters.ClassName))
        {
            query = query.Where(r => r.ClassName == filters.ClassName);
        }

        if (filters.Recommended is not null)
        {
            query = query.Where(r => r.RecommendedInd == filters.Recommended.Value);
        }

        if (filters.AgeMin is not null) query = query.Where(r => r.Age >= filters.AgeMin.Value);
        if (filters.AgeMax is not null) query = query.Where(r => r.Age <= filters.AgeMax.Value);

        if (filters.DateFrom is not null)
        {
            var from = DateTime.SpecifyKind(filters.DateFrom.Value, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt >= from);
        }
        if (filters.DateTo is not null)
        {
            var to = DateTime.SpecifyKind(filters.DateTo.Value, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt <= to);
        }

        if (filters.UploadId is not null)
        {
            query = query.Where(r => r.UploadId == filters.UploadId.Value);
        }

        if (filters.ClothingId is not null)
        {
            query = query.Where(r => r.ClothingId == filters.ClothingId.Value);
        }

        return ApplySorting(query, filters.SortBy, SortOrderParser.Parse(filters.SortOrder));
    }

    private static IQueryable<Review> ApplySorting(IQueryable<Review> query, string? sortBy, SortOrder order)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : sortBy;
        if (!AllowedSortFields.Contains(key))
        {
            key = "createdAt";
        }
        var asc = order == SortOrder.Asc;

        return key.ToLowerInvariant() switch
        {
            "rating" => asc ? query.OrderBy(r => r.Rating) : query.OrderByDescending(r => r.Rating),
            "age" => asc ? query.OrderBy(r => r.Age) : query.OrderByDescending(r => r.Age),
            "overallsentiment" => asc
                ? query.OrderBy(r => r.OverallSentiment)
                : query.OrderByDescending(r => r.OverallSentiment),
            "priority" => asc ? query.OrderBy(r => r.Priority) : query.OrderByDescending(r => r.Priority),
            _ => asc ? query.OrderBy(r => r.CreatedAt) : query.OrderByDescending(r => r.CreatedAt)
        };
    }
}
