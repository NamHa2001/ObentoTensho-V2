using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;

namespace ObentoTensho.Core.Menu.Validators;

public sealed class TranslationRequestValidator : AbstractValidator<TranslationRequest>
{
    public TranslationRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText(150);
        RuleFor(r => r.ShortDesc).OptionalText(200);
        RuleFor(r => r.FullDescHtml).OptionalText(MenuValidators.FullDescMaxLength);
    }
}
