using System.Runtime.Serialization;

namespace ReviewInsights.Api.Common;

public static class EnumParser
{
    public static TEnum? ParseFromMemberName<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            var name = GetEnumMemberValue(enumValue);
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            {
                return enumValue;
            }
        }
        return null;
    }

    public static TEnum[] ParseListFromMemberName<TEnum>(string? csvValue) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csvValue)) return [];
        var tokens = csvValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<TEnum>(tokens.Length);
        foreach (var token in tokens)
        {
            var parsed = ParseFromMemberName<TEnum>(token);
            if (parsed is null)
            {
                throw new ValidationException($"Invalid value '{token}' for {typeof(TEnum).Name}");
            }
            list.Add(parsed.Value);
        }
        return [..list];
    }

    public static string GetEnumMemberValue<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var field = typeof(TEnum).GetField(name);
        var attr = field?.GetCustomAttributes(typeof(EnumMemberAttribute), false)
            .Cast<EnumMemberAttribute>().FirstOrDefault();
        return attr?.Value ?? name;
    }
}
