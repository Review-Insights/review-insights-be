using System.Runtime.Serialization;

namespace ReviewInsights.Api.Domain.Enums;

public enum Sentiment
{
    [EnumMember(Value = "very_negative")]
    VeryNegative,

    [EnumMember(Value = "negative")]
    Negative,

    [EnumMember(Value = "neutral")]
    Neutral,

    [EnumMember(Value = "positive")]
    Positive,

    [EnumMember(Value = "very_positive")]
    VeryPositive
}
