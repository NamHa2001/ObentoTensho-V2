using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Validators;

// Chỉ kiểm hình thức; đúng/sai tài khoản luôn trả INVALID_CREDENTIALS
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Trim().Length <= EmailValidator.MaxLength).WithErrorCode(FieldErrorCodes.TooLong);

        RuleFor(r => r.Password)
            .Must(v => !string.IsNullOrEmpty(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Length <= AuthPolicy.PasswordMaxLength).WithErrorCode(FieldErrorCodes.TooLong);
    }
}
