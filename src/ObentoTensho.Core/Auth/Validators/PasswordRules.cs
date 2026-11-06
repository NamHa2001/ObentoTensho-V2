using FluentValidation;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Validators;

// BR-AUTH-01: ≥ 10 ký tự, không phổ biến/đã lộ, không chứa email; không bắt ký tự đặc biệt
public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule, ICommonPasswordList commonPasswords) =>
        rule
            .Must(v => !string.IsNullOrEmpty(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Length >= AuthPolicy.PasswordMinLength).WithErrorCode(FieldErrorCodes.TooShort)
            .Must(v => v is null || v.Length <= AuthPolicy.PasswordMaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .Must(v => v is null || !commonPasswords.Contains(v)).WithErrorCode(FieldErrorCodes.PasswordTooCommon);

    // Phần trước @ của email, từ 3 ký tự, không phân biệt hoa thường
    public static bool ContainsEmail(string? password, string? email)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var local = EmailAddress.LocalPart(EmailAddress.Normalize(email));
        return local.Length >= 3 && password.Contains(local, StringComparison.OrdinalIgnoreCase);
    }
}
