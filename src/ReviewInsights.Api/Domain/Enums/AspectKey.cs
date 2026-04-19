using System.Runtime.Serialization;

namespace ReviewInsights.Api.Domain.Enums;

public enum AspectKey
{
    [EnumMember(Value = "material")]
    Material,

    [EnumMember(Value = "sizing")]
    Sizing,

    [EnumMember(Value = "fit")]
    Fit,

    [EnumMember(Value = "color")]
    Color,

    [EnumMember(Value = "price")]
    Price
}
