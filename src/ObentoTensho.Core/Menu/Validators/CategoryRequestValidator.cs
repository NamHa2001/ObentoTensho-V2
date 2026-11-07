using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;

namespace ObentoTensho.Core.Menu.Validators;

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText(100);
        RuleFor(r => r.Slug).Slug(150);
    }
}
