namespace ReviewInsights.Api.Common;

public class PaginatedResponse<T>
{
    public IReadOnlyList<T> Data { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPages { get; set; }

    public static PaginatedResponse<T> Create(IReadOnlyList<T> items, int total, int page, int limit)
    {
        return new PaginatedResponse<T>
        {
            Data = items,
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = limit <= 0 ? 0 : (int)Math.Ceiling(total / (double)limit)
        };
    }
}
