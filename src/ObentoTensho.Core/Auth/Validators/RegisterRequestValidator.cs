using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private const int FullNameMaxLength = 100;
    private static readonly DateOnly EarliestDateOfBirth = new(1900, 1, 1);

    public RegisterRequestValidator(ICommonPasswordList commonPasswords, IClock clock)
    {
        RuleFor(r => r.FullName)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Trim().Length <= FullNameMaxLength).WithErrorCode(FieldErrorCodes.TooLong);

        RuleFor(r => r.Email).ValidEmail();

        RuleFor(r => r.PhoneNumber)
            .Must(v => VietnamesePhoneNumber.TryNormalize(v, out _)).WithErrorCode(FieldErrorCodes.InvalidFormat);

        RuleFor(r => r.Password)
            .Must(v => !string.IsNullOrEmpty(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Length >= AuthPolicy.PasswordMinLength).WithErrorCode(FieldErrorCodes.TooShort)
            .Must(v => v is null || v.Length <= AuthPolicy.PasswordMaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .Must(v => v is null || !commonPasswords.Contains(v)).WithErrorCode(FieldErrorCodes.PasswordTooCommon)
            .Must((r, v) => !ContainsEmail(v, r.Email)).WithErrorCode(FieldErrorCodes.PasswordContainsEmail);

        RuleFor(r => r.DateOfBirth)
            .Must(d => d is null || (d.Value >= EarliestDateOfBirth && d.Value <= clock.TodayLocal))
            .WithErrorCode(FieldErrorCodes.OutOfRange);

        RuleFor(r => r.PreferredLang)
            .Must(l => l is null || AuthPolicy.SupportedLanguages.Contains(l, StringComparer.Ordinal))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
    }

    // BR-AUTH-01: mật khẩu không được chứa phần trước @ của email
    private static bool ContainsEmail(string? password, string? email)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var local = EmailAddress.LocalPart(EmailAddress.Normalize(email));
        return local.Length >= 3 && password.Contains(local, StringComparison.OrdinalIgnoreCase);
    }
}
