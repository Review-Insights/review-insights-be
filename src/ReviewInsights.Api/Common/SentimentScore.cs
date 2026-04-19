using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Common;

public static class SentimentScore
{
    public static double ToScore(Sentiment? sentiment) => sentiment switch
    {
        Sentiment.VeryNegative => -1.0,
        Sentiment.Negative => -0.5,
        Sentiment.Neutral => 0.0,
        Sentiment.Positive => 0.5,
        Sentiment.VeryPositive => 1.0,
        _ => 0.0
    };
}
