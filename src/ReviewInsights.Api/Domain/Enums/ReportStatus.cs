using System.Runtime.Serialization;

namespace ReviewInsights.Api.Domain.Enums;

public enum ReportStatus
{
    [EnumMember(Value = "generating")]
    Generating,

    [EnumMember(Value = "completed")]
    Completed,

    [EnumMember(Value = "failed")]
    Failed
}
