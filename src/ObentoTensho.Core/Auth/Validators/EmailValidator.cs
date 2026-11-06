using FluentValidation;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Auth.Validators;

public static class EmailValidator
{
    // Khớp cột varchar(150)
    public const int MaxLength = 150;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(e => !string.IsNullOrWhiteSpace(e)).WithErrorCode(FieldErrorCodes.Required)
            .Must(e => e is null || e.Trim().Length <= MaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .Must(e => e is null || IsValid(e.Trim())).WithErrorCode(FieldErrorCodes.InvalidFormat);

    // Cột email là varchar nên chỉ nhận ASCII; một "@", có phần tên miền chứa dấu chấm
    public static bool IsValid(string email)
    {
        if (email.Length == 0 || !email.All(c => char.IsAscii(c) && !char.IsWhiteSpace(c) && !char.IsControl(c)))
        {
            return false;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != email.LastIndexOf('@'))
        {
            return false;
        }

        var domain = email[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        return dot > 0 && dot < domain.Length - 1 && !domain.Contains("..", StringComparison.Ordinal);
    }
}
