using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Core.Auth.Validators;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator(ICommonPasswordList commonPasswords)
    {
        RuleFor(r => r.Token).Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.NewPassword).StrongPassword(commonPasswords);
    }
}
