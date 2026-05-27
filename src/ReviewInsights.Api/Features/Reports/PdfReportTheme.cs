using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Features.Reports;

internal static class PdfReportTheme
{
    public const string Primary = "#0f172a";
    public const string PrimaryLight = "#1e3a5f";
    public const string Accent = "#2563eb";
    public const string AccentSoft = "#dbeafe";
    public const string Surface = "#f8fafc";
    public const string Border = "#e2e8f0";
    public const string Muted = "#64748b";
    public const string Text = "#334155";

    public static string RatingColor(double rating) => rating switch
    {
        >= 4.0 => "#16a34a",
        >= 3.0 => "#ca8a04",
        _ => "#dc2626"
    };

    public static string RecommendationColor(double rate) => rate switch
    {
        >= 70 => "#16a34a",
        >= 40 => "#ca8a04",
        _ => "#dc2626"
    };

    public static (string Background, string Text) PriorityColors(Priority priority) => priority switch
    {
        Priority.Critical => ("#fee2e2", "#991b1b"),
        Priority.High => ("#ffedd5", "#9a3412"),
        Priority.Medium => ("#fef9c3", "#854d0e"),
        Priority.Low => ("#dcfce7", "#166534"),
        _ => ("#f1f5f9", "#475569")
    };

    public static (string Background, string Text) SentimentColors(Sentiment sentiment) => sentiment switch
    {
        Sentiment.VeryNegative => ("#dc2626", "#ffffff"),
        Sentiment.Negative => ("#f87171", "#ffffff"),
        Sentiment.Neutral => ("#94a3b8", "#ffffff"),
        Sentiment.Positive => ("#4ade80", "#14532d"),
        Sentiment.VeryPositive => ("#16a34a", "#ffffff"),
        _ => ("#e2e8f0", "#334155")
    };

    public static (string Accent, string Label) InsightTypeStyle(InsightType type) => type switch
    {
        InsightType.Trend => ("#2563eb", "Trend"),
        InsightType.Anomaly => ("#dc2626", "Anomalia"),
        InsightType.Pattern => ("#7c3aed", "Wzorzec"),
        _ => ("#64748b", "Insight")
    };

    public static string InsightTypeDisplay(InsightType type) => InsightTypeStyle(type).Label;
}
