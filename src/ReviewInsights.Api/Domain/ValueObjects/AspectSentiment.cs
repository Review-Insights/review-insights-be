using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Domain.ValueObjects;

public class AspectSentiment
{
    public AspectKey Aspect { get; set; }
    public Sentiment Sentiment { get; set; }
    public double Confidence { get; set; }
}
