using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Validators;

public sealed class StaffChangePasswordRequestValidator : AbstractValidator<StaffChangePasswordRequest>
{
    public StaffChangePasswordRequestValidator(ICommonPasswordList commonPasswords)
    {
        RuleFor(r => r.ChallengeToken).Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required);

        RuleFor(r => r.CurrentPassword)
            .Must(v => !string.IsNullOrEmpty(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Length <= AuthPolicy.PasswordMaxLength).WithErrorCode(FieldErrorCodes.TooLong);

        RuleFor(r => r.NewPassword)
            .StrongPassword(commonPasswords)
            .Must((r, v) => !string.Equals(v, r.CurrentPassword, StringComparison.Ordinal)).WithErrorCode(FieldErrorCodes.SameAsCurrent);
    }
}
