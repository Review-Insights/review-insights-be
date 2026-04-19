namespace ReviewInsights.Api.Common;

public static class PaginationParams
{
    public const int DefaultLimit = 20;
    public const int DefaultPage = 1;
    private static readonly int[] AllowedLimits = [10, 20, 50, 100];

    public static (int Page, int Limit) Normalize(int? page, int? limit)
    {
        var normalizedPage = page is null || page < 1 ? DefaultPage : page.Value;
        var normalizedLimit = limit ?? DefaultLimit;
        if (!AllowedLimits.Contains(normalizedLimit))
        {
            normalizedLimit = DefaultLimit;
        }

        return (normalizedPage, normalizedLimit);
    }
}
