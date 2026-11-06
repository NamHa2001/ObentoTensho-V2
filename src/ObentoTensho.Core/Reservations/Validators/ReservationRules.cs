using FluentValidation;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Reservations.Validators;

// Luật trường dùng chung cho khách đặt và lễ tân đặt hộ (BR-RSV-01, BR-RSV-26)
public static class ReservationRules
{
    public const int GuestNameMaxLength = 100;
    public const int TextMaxLength = 500;

    public static IRuleBuilderOptions<T, string> GuestName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Trim().Length <= GuestNameMaxLength).WithErrorCode(FieldErrorCodes.TooLong);

    public static IRuleBuilderOptions<T, string> GuestPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => VietnamesePhoneNumber.TryNormalize(v, out _)).WithErrorCode(FieldErrorCodes.InvalidFormat);

    public static IRuleBuilderOptions<T, string?> PlainText<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || v.Length <= TextMaxLength).WithErrorCode(FieldErrorCodes.TooLong);

    public static IRuleBuilderOptions<T, int> Adults<T>(this IRuleBuilder<T, int> rule, int maxGuests) =>
        rule.InclusiveBetween(1, maxGuests).WithErrorCode(FieldErrorCodes.OutOfRange);

    public static IRuleBuilderOptions<T, int> Children<T>(this IRuleBuilder<T, int> rule, Func<T, int> adults, int maxGuests) =>
        rule.Must((parent, children) => children >= 0 && adults(parent) + children <= maxGuests).WithErrorCode(FieldErrorCodes.OutOfRange);
}
