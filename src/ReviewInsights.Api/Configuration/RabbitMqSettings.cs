namespace ReviewInsights.Api.Configuration;

public class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    public string ExchangeName { get; set; } = "review-insights.exchange";

    public string AnalyzeReviewsQueue { get; set; } = "review-insights.analyze.reviews";
    public string AnalyzeReviewsRoutingKey { get; set; } = "analyze.reviews";

    public string GenerateReportQueue { get; set; } = "review-insights.generate.report";
    public string GenerateReportRoutingKey { get; set; } = "generate.report";

    public int BatchSize { get; set; } = 50;
}
