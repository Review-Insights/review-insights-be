using System.Runtime.Serialization;

namespace ReviewInsights.Api.Domain.Enums;

public enum InsightType
{
    [EnumMember(Value = "trend")]
    Trend,

    [EnumMember(Value = "anomaly")]
    Anomaly,

    [EnumMember(Value = "pattern")]
    Pattern
}
