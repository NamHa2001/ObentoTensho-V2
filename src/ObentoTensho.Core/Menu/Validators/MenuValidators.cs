using FluentValidation;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Menu.Validators;

// Luật trường dùng chung cho các validator thực đơn
public static class MenuValidators
{
    public const int FullDescMaxLength = 20_000;

    public static IRuleBuilderOptions<T, string> RequiredText<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Trim().Length <= maxLength).WithErrorCode(FieldErrorCodes.TooLong);

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.Must(v => v is null || v.Length <= maxLength).WithErrorCode(FieldErrorCodes.TooLong);

    // Slug do người dùng sửa: chữ thường không dấu, số, gạch nối
    public static IRuleBuilderOptions<T, string?> Slug<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.Must(v => v is null || (v.Length is > 0 && v.Length <= maxLength && v.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') && !v.StartsWith('-') && !v.EndsWith('-')))
            .WithErrorCode(FieldErrorCodes.InvalidFormat);

    // Giá VND là số nguyên dương
    public static IRuleBuilderOptions<T, decimal> Money<T>(this IRuleBuilder<T, decimal> rule, decimal min, decimal max) =>
        rule.Must(v => v >= min && v <= max && decimal.Truncate(v) == v).WithErrorCode(FieldErrorCodes.OutOfRange);

    public static IRuleBuilderOptions<T, IReadOnlyList<Allergen>> Allergens<T>(this IRuleBuilder<T, IReadOnlyList<Allergen>> rule) =>
        rule.Must(list => list is null || list.All(Enum.IsDefined)).WithErrorCode(FieldErrorCodes.OutOfRange);

    public static IRuleBuilderOptions<T, SaleStatus> Status<T>(this IRuleBuilder<T, SaleStatus> rule) =>
        rule.IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
}
