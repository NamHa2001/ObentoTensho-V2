using FluentValidation;
using ObentoTensho.Contracts.Dtos.Account;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Validators;
using ObentoTensho.Core.Orders.Validators;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Customers.Validators;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly DateOnly EarliestDateOfBirth = new(1900, 1, 1);

    public UpdateProfileRequestValidator(IClock clock)
    {
        RuleFor(r => r.FullName).RequiredText(100).NoMarkup();
        RuleFor(r => r.PhoneNumber)
            .Must(v => VietnamesePhoneNumber.TryNormalize(v, out _)).WithErrorCode(FieldErrorCodes.InvalidFormat);
        RuleFor(r => r.DateOfBirth)
            .Must(d => d is null || (d.Value >= EarliestDateOfBirth && d.Value <= clock.TodayLocal))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.PreferredLang).Language();
    }
}
