using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Dtos.Staff;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Auth.Validators;

public sealed class ChangeOwnPasswordRequestValidator : AbstractValidator<ChangeOwnPasswordRequest>
{
    public ChangeOwnPasswordRequestValidator(ICommonPasswordList commonPasswords)
    {
        RuleFor(r => r.CurrentPassword)
            .Must(v => !string.IsNullOrEmpty(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Length <= AuthPolicy.PasswordMaxLength).WithErrorCode(FieldErrorCodes.TooLong);

        RuleFor(r => r.NewPassword)
            .StrongPassword(commonPasswords)
            .Must((r, v) => !string.Equals(v, r.CurrentPassword, StringComparison.Ordinal)).WithErrorCode(FieldErrorCodes.SameAsCurrent);
    }
}
