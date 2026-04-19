using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Domain.Entities;
using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Features.Reports;

public class PdfReportRenderer
{
    public byte[] Render(Report report)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(report.Title).FontSize(20).SemiBold();
                    col.Item().Text($"Wygenerowano: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Status: {EnumParser.GetEnumMemberValue(report.Status)} | Rekordow: {report.TotalRecords}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(15);

                    RenderFilters(col, report.Filters);

                    if (report.Summary is not null)
                    {
                        RenderSummary(col, report.Summary);
                    }

                    if (report.Insights.Count > 0)
                    {
                        RenderInsights(col, report.Insights);
                    }

                    if (report.Suggestions.Count > 0)
                    {
                        RenderSuggestions(col, report.Suggestions);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Strona ").FontSize(9);
                    t.CurrentPageNumber().FontSize(9);
                    t.Span(" z ").FontSize(9);
                    t.TotalPages().FontSize(9);
                });
            });
        }).GeneratePdf();
    }

    private static void RenderFilters(ColumnDescriptor col, ReportFilters f)
    {
        col.Item().Text("Filtry").FontSize(14).SemiBold();
        col.Item().Column(c =>
        {
            if (f.DateFrom is not null) c.Item().Text($"Data od: {f.DateFrom:yyyy-MM-dd}");
            if (f.DateTo is not null) c.Item().Text($"Data do: {f.DateTo:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(f.DepartmentName)) c.Item().Text($"Departament: {f.DepartmentName}");
            if (!string.IsNullOrWhiteSpace(f.DivisionName)) c.Item().Text($"Dywizja: {f.DivisionName}");
            if (!string.IsNullOrWhiteSpace(f.ClassName)) c.Item().Text($"Klasa produktu: {f.ClassName}");
            if (f.ClothingId is not null) c.Item().Text($"Produkt ID: {f.ClothingId}");
            if (f.MinRating is not null) c.Item().Text($"Min. ocena: {f.MinRating}");
            if (f.MaxRating is not null) c.Item().Text($"Max. ocena: {f.MaxRating}");
        });
    }

    private static void RenderSummary(ColumnDescriptor col, ReportSummary s)
    {
        col.Item().Text("Podsumowanie").FontSize(14).SemiBold();
        col.Item().Column(c =>
        {
            c.Item().Text($"Srednia ocena: {s.AverageRating:F2} / 5");
            c.Item().Text($"Wskaznik rekomendacji: {s.RecommendationRate:F1}%");
            c.Item().PaddingTop(5).Text("Rozklad sentymentu:").SemiBold();
            foreach (var kvp in s.SentimentBreakdown)
            {
                c.Item().Text($"  {EnumParser.GetEnumMemberValue(kvp.Key)}: {kvp.Value}");
            }
            if (s.TopChurnCauses.Count > 0)
            {
                c.Item().PaddingTop(5).Text("Najczestsze przyczyny churn:").SemiBold();
                foreach (var cause in s.TopChurnCauses)
                {
                    c.Item().Text($"  {EnumParser.GetEnumMemberValue(cause.Cause)}: {cause.Count}");
                }
            }
        });
    }

    private static void RenderInsights(ColumnDescriptor col, IEnumerable<ReportInsight> insights)
    {
        col.Item().Text("Insighty").FontSize(14).SemiBold();
        foreach (var i in insights)
        {
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text($"[{EnumParser.GetEnumMemberValue(i.Type)}] {i.Title}").SemiBold();
                c.Item().Text($"Waznosc: {EnumParser.GetEnumMemberValue(i.Severity)}").FontSize(9).FontColor(Colors.Grey.Darken1);
                c.Item().PaddingTop(4).Text(i.Description);
            });
        }
    }

    private static void RenderSuggestions(ColumnDescriptor col, IEnumerable<ReportSuggestion> suggestions)
    {
        col.Item().Text("Sugerowane dzialania").FontSize(14).SemiBold();
        foreach (var s in suggestions)
        {
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text(s.Action).SemiBold();
                c.Item().Text($"Priorytet: {EnumParser.GetEnumMemberValue(s.Priority)}").FontSize(9).FontColor(Colors.Grey.Darken1);
                c.Item().PaddingTop(4).Text(s.Reasoning);
                if (s.RelatedProducts.Count > 0)
                {
                    c.Item().PaddingTop(4).Text($"Powiazane produkty: {string.Join(", ", s.RelatedProducts)}").FontSize(9);
                }
            });
        }
    }
}
