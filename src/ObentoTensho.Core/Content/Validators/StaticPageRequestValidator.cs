using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class StaticPageRequestValidator : AbstractValidator<StaticPageRequest>
{
    public StaticPageRequestValidator()
    {
        RuleFor(r => r.Title).RequiredText(200);
        RuleFor(r => r.ContentHtml).RequiredText(NewsRequestValidator.ContentMaxLength);
    }
}
