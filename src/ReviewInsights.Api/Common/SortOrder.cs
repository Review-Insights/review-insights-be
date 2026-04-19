namespace ReviewInsights.Api.Common;

public enum SortOrder
{
    Asc,
    Desc
}

public static class SortOrderParser
{
    public static SortOrder Parse(string? value, SortOrder fallback = SortOrder.Desc)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return value.Equals("asc", StringComparison.OrdinalIgnoreCase) ? SortOrder.Asc : SortOrder.Desc;
    }
}
