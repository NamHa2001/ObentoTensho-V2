using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class NewsletterCampaignRequestValidator : AbstractValidator<NewsletterCampaignRequest>
{
    public NewsletterCampaignRequestValidator()
    {
        RuleFor(r => r.Subject).RequiredText(150);
        RuleFor(r => r.ContentHtml).RequiredText(NewsRequestValidator.ContentMaxLength);
    }
}
