using System.Globalization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Validators;

namespace ObentoTensho.Core.Settings.Domain;

// BR-RPT-21: kiểm kiểu và khoảng giá trị khi ghi; trả mã lỗi trường, null = hợp lệ
public static class SettingRules
{
    public const int TextMaxLength = 1000;

    public static string? Check(SettingDefinition definition, string value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > TextMaxLength)
        {
            return FieldErrorCodes.TooLong;
        }

        return definition.Type switch
        {
            SettingType.Number => CheckInt(definition, value),
            SettingType.Percent => CheckPercent(definition, value),
            SettingType.Bool => value is "true" or "false" ? null : FieldErrorCodes.InvalidFormat,
            SettingType.Url => value.Length == 0 || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)) ? null : FieldErrorCodes.InvalidFormat,
            SettingType.Email => value.Length == 0 || EmailValidator.IsValid(value) ? null : FieldErrorCodes.InvalidFormat,
            SettingType.Time => CheckTime(definition, value),
            SettingType.Choice => definition.Choices!.Contains(value, StringComparer.Ordinal) ? null : FieldErrorCodes.OutOfRange,
            SettingType.List => CheckList(definition, value),
            SettingType.Image => value.Length == 0 || Guid.TryParse(value, out _) ? null : FieldErrorCodes.InvalidFormat,
            _ => null,
        };
    }

    private static string? CheckInt(SettingDefinition definition, string value)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            return FieldErrorCodes.InvalidFormat;
        }

        return number < definition.Min || number > definition.Max ? FieldErrorCodes.OutOfRange : null;
    }

    // Phần trăm: số không âm, tối đa 2 chữ số thập phân, dấu chấm thập phân
    private static string? CheckPercent(SettingDefinition definition, string value)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || decimal.Round(number, 2) != number)
        {
            return FieldErrorCodes.InvalidFormat;
        }

        return number < definition.Min || number > definition.Max ? FieldErrorCodes.OutOfRange : null;
    }

    // Max tính bằng giờ
    private static string? CheckTime(SettingDefinition definition, string value)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return FieldErrorCodes.InvalidFormat;
        }

        return time.ToTimeSpan().TotalHours > (double)(definition.Max ?? 24) ? FieldErrorCodes.OutOfRange : null;
    }

    private static string? CheckList(SettingDefinition definition, string value)
    {
        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Length > 0 && items.All(i => definition.Choices!.Contains(i, StringComparer.Ordinal)) ? null : FieldErrorCodes.OutOfRange;
    }
}
