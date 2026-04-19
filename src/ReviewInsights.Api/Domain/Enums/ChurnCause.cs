using System.Runtime.Serialization;

namespace ReviewInsights.Api.Domain.Enums;

public enum ChurnCause
{
    [EnumMember(Value = "poor_quality")]
    PoorQuality,

    [EnumMember(Value = "sizing_issues")]
    SizingIssues,

    [EnumMember(Value = "price_too_high")]
    PriceTooHigh,

    [EnumMember(Value = "style_mismatch")]
    StyleMismatch,

    [EnumMember(Value = "defective_product")]
    DefectiveProduct
}
