using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Core.Auth.Validators;

public sealed class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator(ICommonPasswordList commonPasswords)
    {
        RuleFor(r => r.Token).Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.Password).StrongPassword(commonPasswords);
    }
}
