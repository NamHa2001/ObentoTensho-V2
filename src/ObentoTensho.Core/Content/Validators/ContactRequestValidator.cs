using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Validators;
using ObentoTensho.Core.Orders.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class ContactRequestValidator : AbstractValidator<ContactRequest>
{
    public ContactRequestValidator()
    {
        RuleFor(r => r.FullName).RequiredText(100).NoMarkup();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.PhoneNumber).OptionalText(20);
        RuleFor(r => r.Subject).OptionalText(200);
        RuleFor(r => r.Message).RequiredText(2000);
        RuleFor(r => r.Lang).Language();
    }
}
