using FluentValidation;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.Core.Orders.Validators;

// BR-ORD-01: giỏ 1..maxLines dòng, mỗi dòng 1–99 phần; ghi chú văn bản thuần có giới hạn
public static class OrderRules
{
    // Mỗi món tối đa 10 nhóm × 20 tùy chọn (BR-MNU-18)
    public const int MaxOptionIdsPerLine = 200;

    public static IRuleBuilderOptions<T, IReadOnlyList<OrderLineRequest>> Lines<T>(this IRuleBuilder<T, IReadOnlyList<OrderLineRequest>> rule, int maxLines) =>
        rule
            .Must(lines => lines is { Count: > 0 }).WithErrorCode(FieldErrorCodes.Required)
            .Must(lines => lines is null || lines.Count <= maxLines).WithErrorCode(FieldErrorCodes.OutOfRange)
            .Must(lines => lines is null || lines.All(ValidLine)).WithErrorCode(FieldErrorCodes.OutOfRange);

    public static IRuleBuilderOptions<T, string?> Language<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(l => l is null || AuthPolicy.SupportedLanguages.Contains(l, StringComparer.Ordinal)).WithErrorCode(FieldErrorCodes.OutOfRange);

    private static bool ValidLine(OrderLineRequest? line) =>
        line is not null
        && line.MenuItemId > 0
        && line.Quantity is >= 1 and <= OrderCart.MaxQuantity
        && (line.Note is null || line.Note.Length <= OrderCart.NoteMaxLength)
        && (line.OptionIds is null || line.OptionIds.Count <= MaxOptionIdsPerLine);
}
