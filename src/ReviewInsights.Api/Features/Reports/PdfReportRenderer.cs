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
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(PdfReportTheme.Text));

                page.Header().Element(c => RenderPageHeader(c, report));
                page.Footer().Element(RenderPageFooter);

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Spacing(18);

                    RenderHero(col, report);
                    RenderFilters(col, report.Filters);

                    if (report.Scope is not null)
                    {
                        RenderScope(col, report.Scope);
                    }

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
            });
        }).GeneratePdf();
    }

    private static void RenderPageHeader(IContainer container, Report report)
    {
        container.Column(col =>
        {
            col.Item().Height(4).Background(PdfReportTheme.Accent);
            col.Item().Background(PdfReportTheme.Primary).PaddingVertical(10).PaddingHorizontal(14).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Review Insights").FontSize(8).FontColor("#94a3b8");
                    left.Item().Text(report.Title).FontSize(13).SemiBold().FontColor(Colors.White);
                });
                row.ConstantItem(120).AlignRight().AlignMiddle().Column(right =>
                {
                    right.Item().AlignRight()
                        .Text($"{report.GeneratedAt:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(8).FontColor("#cbd5e1");
                    right.Item().AlignRight()
                        .Text($"{report.TotalRecords} rekordow")
                        .FontSize(8).FontColor("#cbd5e1");
                });
            });
        });
    }

    private static void RenderPageFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(PdfReportTheme.Border).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("Review Insights — raport analityczny")
                .FontSize(8).FontColor(PdfReportTheme.Muted);
            row.ConstantItem(80).AlignRight().Text(t =>
            {
                t.Span("Strona ").FontSize(8).FontColor(PdfReportTheme.Muted);
                t.CurrentPageNumber().FontSize(8).FontColor(PdfReportTheme.Muted);
                t.Span(" / ").FontSize(8).FontColor(PdfReportTheme.Muted);
                t.TotalPages().FontSize(8).FontColor(PdfReportTheme.Muted);
            });
        });
    }

    private static void RenderHero(ColumnDescriptor col, Report report)
    {
        col.Item().Background(PdfReportTheme.Surface).Border(1).BorderColor(PdfReportTheme.Border)
            .Padding(16).Column(hero =>
            {
                hero.Item().Text(report.Title).FontSize(22).Bold().FontColor(PdfReportTheme.Primary);
                hero.Item().PaddingTop(6).Text(text =>
                {
                    text.Span("Status: ").FontColor(PdfReportTheme.Muted);
                    text.Span(EnumParser.GetEnumMemberValue(report.Status)).SemiBold();
                    text.Span("  ·  ").FontColor(PdfReportTheme.Muted);
                    text.Span("Wygenerowano: ").FontColor(PdfReportTheme.Muted);
                    text.Span($"{report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").SemiBold();
                });
            });
    }

    private static void SectionTitle(ColumnDescriptor col, string title, string? subtitle = null)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(4).Height(22).Background(PdfReportTheme.Accent);
            row.RelativeItem().PaddingLeft(10).Column(c =>
            {
                c.Item().Text(title).FontSize(14).SemiBold().FontColor(PdfReportTheme.Primary);
                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    c.Item().Text(subtitle).FontSize(9).FontColor(PdfReportTheme.Muted);
                }
            });
        });
    }

    private static void RenderScope(ColumnDescriptor col, ReportScope scope)
    {
        col.Item().Column(section =>
        {
            SectionTitle(section, "Zakres raportu");
            section.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Element(c => MetricCard(c, "Przeanalizowane", scope.AnalyzedReviewCount.ToString(), PdfReportTheme.Accent));
                row.ConstantItem(10);
                row.RelativeItem().Element(c => MetricCard(c, "Pominiete", scope.SkippedReviewCount.ToString(), PdfReportTheme.Muted));
            });
        });
    }

    private static void RenderFilters(ColumnDescriptor col, ReportFilters f)
    {
        col.Item().Column(section =>
        {
            var hasFilters = HasActiveFilters(f);
            SectionTitle(
                section,
                "Filtry",
                hasFilters ? "Kryteria wyboru danych do raportu" : "Brak — uwzgledniono wszystkie dostepne recenzje");

            section.Item().PaddingTop(10).Background(PdfReportTheme.Surface).Border(1)
                .BorderColor(PdfReportTheme.Border).Padding(12).Column(c =>
                {
                    if (!hasFilters)
                    {
                        c.Item().Text("BRAK").FontSize(12).Bold().FontColor(PdfReportTheme.Muted);
                        c.Item().PaddingTop(4).Text("Nie ustawiono zadnych filtrow.")
                            .FontSize(9).Italic().FontColor(PdfReportTheme.Muted);
                        return;
                    }

                    if (f.DateFrom is not null) c.Item().Element(x => FilterChip(x, "Od", f.DateFrom.Value.ToString("yyyy-MM-dd")));
                    if (f.DateTo is not null) c.Item().Element(x => FilterChip(x, "Do", f.DateTo.Value.ToString("yyyy-MM-dd")));
                    if (!string.IsNullOrWhiteSpace(f.DepartmentName)) c.Item().Element(x => FilterChip(x, "Departament", f.DepartmentName));
                    if (!string.IsNullOrWhiteSpace(f.DivisionName)) c.Item().Element(x => FilterChip(x, "Dywizja", f.DivisionName));
                    if (!string.IsNullOrWhiteSpace(f.ClassName)) c.Item().Element(x => FilterChip(x, "Klasa", f.ClassName));
                    if (f.ClothingId is not null) c.Item().Element(x => FilterChip(x, "Produkt", $"#{f.ClothingId}"));
                    if (f.MinRating is not null || f.MaxRating is not null)
                    {
                        var range = $"{f.MinRating ?? 1} – {f.MaxRating ?? 5}";
                        c.Item().Element(x => FilterChip(x, "Ocena", range));
                    }
                });
        });
    }

    private static bool HasActiveFilters(ReportFilters f) =>
        f.DateFrom is not null
        || f.DateTo is not null
        || !string.IsNullOrWhiteSpace(f.DepartmentName)
        || !string.IsNullOrWhiteSpace(f.DivisionName)
        || !string.IsNullOrWhiteSpace(f.ClassName)
        || f.ClothingId is not null
        || f.MinRating is not null
        || f.MaxRating is not null;

    private static void FilterChip(IContainer container, string label, string value)
    {
        container.PaddingBottom(4).Row(row =>
        {
            row.ConstantItem(90).Text(label).FontSize(9).FontColor(PdfReportTheme.Muted);
            row.RelativeItem().Text(value).FontSize(10).SemiBold();
        });
    }

    private static void RenderSummary(ColumnDescriptor col, ReportSummary s)
    {
        col.Item().Column(section =>
        {
            SectionTitle(section, "Podsumowanie", "Kluczowe metryki z przeanalizowanych opinii");
            section.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Element(c => MetricCard(c, "Recenzje", s.TotalReviews.ToString(), PdfReportTheme.Accent));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => MetricCard(c, "Srednia ocena", $"{s.AverageRating:F2}", PdfReportTheme.RatingColor(s.AverageRating), "/ 5"));
                row.ConstantItem(8);
                row.RelativeItem().Element(c => MetricCard(c, "Rekomendacja", $"{s.RecommendationRate:F1}%", PdfReportTheme.RecommendationColor(s.RecommendationRate)));
            });

            if (s.SentimentBreakdown.Count > 0)
            {
                section.Item().PaddingTop(14).Text("Rozklad sentymentu").FontSize(11).SemiBold()
                    .FontColor(PdfReportTheme.Primary);
                section.Item().PaddingTop(6).Element(c => RenderBreakdownBars(c, s.SentimentBreakdown
                    .ToDictionary(k => EnumParser.GetEnumMemberValue(k.Key), v => v.Value), SentimentBarColor));
            }

            if (s.PriorityBreakdown.Count > 0)
            {
                section.Item().PaddingTop(14).Text("Rozklad priorytetow").FontSize(11).SemiBold()
                    .FontColor(PdfReportTheme.Primary);
                section.Item().PaddingTop(6).Element(c => RenderBreakdownBars(c, s.PriorityBreakdown
                    .ToDictionary(k => EnumParser.GetEnumMemberValue(k.Key), v => v.Value), PriorityBarColor));
            }

            if (s.TopProblemProducts.Count > 0)
            {
                section.Item().PaddingTop(14).Text("Produkty najwyzszego ryzyka").FontSize(11).SemiBold()
                    .FontColor("#dc2626");
                foreach (var product in s.TopProblemProducts)
                {
                    section.Item().PaddingTop(6).Element(c => RenderProductCard(c, product, isRisk: true));
                }
            }

            if (s.TopOpportunityProducts.Count > 0)
            {
                section.Item().PaddingTop(14).Text("Produkty z najwieksza szansa").FontSize(11).SemiBold()
                    .FontColor("#16a34a");
                foreach (var product in s.TopOpportunityProducts)
                {
                    section.Item().PaddingTop(6).Element(c => RenderProductCard(c, product, isRisk: false));
                }
            }
        });
    }

    private static string SentimentBarColor(string key) => key switch
    {
        "very_negative" => "#dc2626",
        "negative" => "#f87171",
        "neutral" => "#94a3b8",
        "positive" => "#4ade80",
        "very_positive" => "#16a34a",
        _ => PdfReportTheme.Border
    };

    private static string PriorityBarColor(string key) => key switch
    {
        "critical" => "#dc2626",
        "high" => "#ea580c",
        "medium" => "#ca8a04",
        "low" => "#16a34a",
        _ => PdfReportTheme.Border
    };

    private static void RenderBreakdownBars(IContainer container, Dictionary<string, int> breakdown, Func<string, string> colorForKey)
    {
        var total = breakdown.Values.Sum();
        if (total == 0) return;

        container.Column(col =>
        {
            foreach (var (label, count) in breakdown.OrderByDescending(x => x.Value))
            {
                var ratio = (float)count / total;
                col.Item().PaddingBottom(6).Column(row =>
                {
                    row.Item().Row(header =>
                    {
                        header.RelativeItem().Text(label).FontSize(9).SemiBold();
                        header.ConstantItem(60).AlignRight().Text($"{count} ({ratio:P0})").FontSize(9)
                            .FontColor(PdfReportTheme.Muted);
                    });
                    row.Item().PaddingTop(3).Height(8).Row(bar =>
                    {
                        if (ratio > 0)
                        {
                            bar.RelativeItem(ratio).Background(colorForKey(label));
                        }
                        if (ratio < 1)
                        {
                            bar.RelativeItem(1 - ratio).Background(PdfReportTheme.Border);
                        }
                    });
                });
            }
        });
    }

    private static void RenderProductCard(IContainer container, ReportProductSummary product, bool isRisk)
    {
        var accent = isRisk ? "#fecaca" : "#bbf7d0";
        container.Background(PdfReportTheme.Surface).Border(1).BorderColor(PdfReportTheme.Border)
            .Row(row =>
            {
                row.ConstantItem(4).Background(isRisk ? "#dc2626" : "#16a34a");
                row.RelativeItem().Padding(10).Column(c =>
                {
                    c.Item().Row(title =>
                    {
                        title.RelativeItem().Text($"Produkt #{product.ClothingId}").FontSize(11).SemiBold();
                        title.ConstantItem(80).AlignRight().Background(accent).PaddingHorizontal(6).PaddingVertical(2)
                            .Text(isRisk ? "Ryzyko" : "Szansa").FontSize(8).SemiBold()
                            .FontColor(isRisk ? "#991b1b" : "#166534");
                    });
                    c.Item().PaddingTop(4).Row(metrics =>
                    {
                        metrics.RelativeItem().Element(x => MiniMetric(x, "Ocena", $"{product.AverageRating:F2}",
                            PdfReportTheme.RatingColor(product.AverageRating)));
                        metrics.ConstantItem(8);
                        metrics.RelativeItem().Element(x => MiniMetric(x, "Rekomendacja",
                            $"{product.RecommendationRate:F0}%",
                            PdfReportTheme.RecommendationColor(product.RecommendationRate)));
                        metrics.ConstantItem(8);
                        metrics.RelativeItem().Element(x => MiniMetric(x, "Negatywne",
                            $"{product.NegativeReviewRate:F0}%", isRisk ? "#dc2626" : PdfReportTheme.Muted));
                    });
                    if (!string.IsNullOrWhiteSpace(product.DepartmentName) || !string.IsNullOrWhiteSpace(product.ClassName))
                    {
                        c.Item().PaddingTop(4).Text(
                                $"{product.DepartmentName} · {product.ClassName}")
                            .FontSize(8).FontColor(PdfReportTheme.Muted);
                    }
                });
            });
    }

    private static void RenderInsights(ColumnDescriptor col, IEnumerable<ReportInsight> insights)
    {
        col.Item().Column(section =>
        {
            SectionTitle(section, "Insighty", "Wnioski z analizy AI");
            foreach (var insight in insights)
            {
                section.Item().PaddingTop(10).Element(c => RenderInsightCard(c, insight));
            }
        });
    }

    private static void RenderInsightCard(IContainer container, ReportInsight insight)
    {
        var (accent, typeLabel) = PdfReportTheme.InsightTypeStyle(insight.Type);
        var (prioBg, prioFg) = PdfReportTheme.PriorityColors(insight.Severity);

        container.Background(Colors.White).Border(1).BorderColor(PdfReportTheme.Border).Column(card =>
        {
            card.Item().Row(header =>
            {
                header.ConstantItem(4).Background(accent);
                header.RelativeItem().Background(PdfReportTheme.Surface).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(typeLabel).FontSize(8).Bold().FontColor(accent);
                        c.Item().Text(insight.Title).FontSize(12).SemiBold().FontColor(PdfReportTheme.Primary);
                    });
                    row.ConstantItem(70).AlignRight().AlignMiddle().Background(prioBg).Padding(4)
                        .Text(EnumParser.GetEnumMemberValue(insight.Severity)).FontSize(8).Bold().FontColor(prioFg);
                });
            });
            card.Item().Padding(12).Column(body =>
            {
                body.Item().Text(insight.Description).FontSize(10).LineHeight(1.4f);
                if (!string.IsNullOrWhiteSpace(insight.TargetSegment))
                {
                    body.Item().PaddingTop(6).Text(t =>
                    {
                        t.Span("Segment: ").FontSize(8).FontColor(PdfReportTheme.Muted);
                        t.Span(insight.TargetSegment).FontSize(9).SemiBold();
                    });
                }
                if (insight.RelatedProducts.Count > 0)
                {
                    body.Item().PaddingTop(6).Text(t =>
                    {
                        t.Span("Produkty: ").FontSize(8).FontColor(PdfReportTheme.Muted);
                        t.Span(string.Join(", ", insight.RelatedProducts.Select(p => $"#{p}"))).FontSize(9);
                    });
                }
                RenderEvidence(body, insight.Evidence);
            });
        });
    }

    private static void RenderSuggestions(ColumnDescriptor col, IEnumerable<ReportSuggestion> suggestions)
    {
        col.Item().Column(section =>
        {
            SectionTitle(section, "Sugerowane dzialania", "Rekomendacje operacyjne");
            foreach (var suggestion in suggestions)
            {
                section.Item().PaddingTop(10).Element(c => RenderSuggestionCard(c, suggestion));
            }
        });
    }

    private static void RenderSuggestionCard(IContainer container, ReportSuggestion suggestion)
    {
        var (prioBg, prioFg) = PdfReportTheme.PriorityColors(suggestion.Priority);

        container.Border(1).BorderColor(PdfReportTheme.Border).Column(card =>
        {
            card.Item().Background(prioBg).Padding(10).Row(header =>
            {
                header.RelativeItem().Text(suggestion.Action).FontSize(12).SemiBold().FontColor(prioFg);
                header.ConstantItem(70).AlignRight().Background(Colors.White).Padding(4)
                    .Text(EnumParser.GetEnumMemberValue(suggestion.Priority)).FontSize(8).Bold().FontColor(prioFg);
            });
            card.Item().Padding(12).Column(body =>
            {
                if (!string.IsNullOrWhiteSpace(suggestion.TargetSegment))
                {
                    body.Item().Text(t =>
                    {
                        t.Span("Segment: ").FontColor(PdfReportTheme.Muted).FontSize(8);
                        t.Span(suggestion.TargetSegment).SemiBold().FontSize(9);
                    });
                }
                body.Item().PaddingTop(4).Text(suggestion.Reasoning).FontSize(10).LineHeight(1.4f);
                if (!string.IsNullOrWhiteSpace(suggestion.ExpectedImpact))
                {
                    body.Item().PaddingTop(8).Background(PdfReportTheme.AccentSoft).Padding(8).Column(impact =>
                    {
                        impact.Item().Text("Oczekiwany efekt").FontSize(8).Bold().FontColor(PdfReportTheme.Accent);
                        impact.Item().Text(suggestion.ExpectedImpact).FontSize(9);
                    });
                }
                if (suggestion.RelatedProducts.Count > 0)
                {
                    body.Item().PaddingTop(6).Text(t =>
                    {
                        t.Span("Produkty: ").FontSize(8).FontColor(PdfReportTheme.Muted);
                        t.Span(string.Join(", ", suggestion.RelatedProducts.Select(p => $"#{p}"))).FontSize(9);
                    });
                }
                RenderEvidence(body, suggestion.Evidence);
            });
        });
    }

    private static void MetricCard(IContainer container, string label, string value, string valueColor, string? suffix = null)
    {
        container.Background(PdfReportTheme.Surface).Border(1).BorderColor(PdfReportTheme.Border).Padding(12)
            .Column(col =>
            {
                col.Item().Text(label).FontSize(8).FontColor(PdfReportTheme.Muted);
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.AutoItem().Text(value).FontSize(18).Bold().FontColor(valueColor);
                    if (!string.IsNullOrWhiteSpace(suffix))
                    {
                        row.AutoItem().PaddingLeft(4).AlignBottom().Text(suffix).FontSize(10)
                            .FontColor(PdfReportTheme.Muted);
                    }
                });
            });
    }

    private static void MiniMetric(IContainer container, string label, string value, string valueColor)
    {
        container.Column(col =>
        {
            col.Item().Text(label).FontSize(7).FontColor(PdfReportTheme.Muted);
            col.Item().Text(value).FontSize(10).Bold().FontColor(valueColor);
        });
    }

    private static void RenderEvidence(ColumnDescriptor col, IEnumerable<ReportEvidence> evidence)
    {
        var items = evidence.Where(item =>
            !string.IsNullOrWhiteSpace(item.Label) || !string.IsNullOrWhiteSpace(item.Detail)).ToList();
        if (items.Count == 0) return;

        col.Item().PaddingTop(8).Text("Dowody").FontSize(8).Bold().FontColor(PdfReportTheme.Muted);
        foreach (var item in items)
        {
            col.Item().PaddingTop(4).Background(PdfReportTheme.Surface).BorderLeft(2)
                .BorderColor(PdfReportTheme.Accent).Padding(6).Text(text =>
                {
                    if (!string.IsNullOrWhiteSpace(item.Label))
                    {
                        text.Span(item.Label).SemiBold().FontSize(8);
                        text.Span(": ").FontSize(8);
                    }
                    text.Span(item.Detail ?? "").FontSize(8);
                });
        }
    }
}
