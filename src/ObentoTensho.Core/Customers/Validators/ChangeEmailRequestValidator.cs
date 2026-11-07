using FluentValidation;
using ObentoTensho.Contracts.Dtos.Account;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Validators;

namespace ObentoTensho.Core.Customers.Validators;

public sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
    public ChangeEmailRequestValidator()
    {
        RuleFor(r => r.CurrentPassword)
            .Must(v => !string.IsNullOrEmpty(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Length <= AuthPolicy.PasswordMaxLength).WithErrorCode(FieldErrorCodes.TooLong);
        RuleFor(r => r.NewEmail).ValidEmail();
    }
}
