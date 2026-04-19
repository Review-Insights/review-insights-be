using System.Runtime.Serialization;

namespace ReviewInsights.Api.Domain.Enums;

public enum Priority
{
    [EnumMember(Value = "low")]
    Low,

    [EnumMember(Value = "medium")]
    Medium,

    [EnumMember(Value = "high")]
    High,

    [EnumMember(Value = "critical")]
    Critical
}
