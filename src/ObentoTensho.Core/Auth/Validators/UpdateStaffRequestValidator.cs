using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Dtos.Staff;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Auth.Validators;

public sealed class UpdateStaffRequestValidator : AbstractValidator<UpdateStaffRequest>
{
    public UpdateStaffRequestValidator()
    {
        RuleFor(r => r.FullName).RequiredText(100).NoMarkup();
        RuleFor(r => r.Role).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
