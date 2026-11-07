using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class NewsCategoryRequestValidator : AbstractValidator<NewsCategoryRequest>
{
    public NewsCategoryRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText(100);
        RuleFor(r => r.Slug).Slug(150);
    }
}
