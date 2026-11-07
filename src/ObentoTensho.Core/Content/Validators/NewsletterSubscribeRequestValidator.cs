using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Orders.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class NewsletterSubscribeRequestValidator : AbstractValidator<NewsletterSubscribeRequest>
{
    public NewsletterSubscribeRequestValidator()
    {
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Lang).Language();
    }
}
