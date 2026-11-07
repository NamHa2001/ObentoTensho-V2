using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Validators;

// BR-MNU-05: giá > 0 số nguyên; giá gốc (nếu có) > giá bán
public sealed class MenuItemRequestValidator : AbstractValidator<MenuItemRequest>
{
    public MenuItemRequestValidator()
    {
        RuleFor(r => r.CategoryId).GreaterThan(0).WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.NameVi).RequiredText(150);
        RuleFor(r => r.Slug).Slug(200);
        RuleFor(r => r.ShortDesc).OptionalText(200);
        RuleFor(r => r.FullDescHtml).OptionalText(MenuValidators.FullDescMaxLength);
        RuleFor(r => r.Price).Money(1, MenuRules.MaxPrice);
        RuleFor(r => r.OriginalPrice)
            .Must((r, original) => original is null || (original > r.Price && original <= MenuRules.MaxPrice && decimal.Truncate(original.Value) == original))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Labels).NotNull().WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.Status).Status();
        RuleFor(r => r.Allergens).Allergens();
        RuleFor(r => r.OptionGroupIds)
            .Must(ids => ids is null || (ids.Count <= MenuRules.MaxGroupsPerItem && ids.Distinct().Count() == ids.Count))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
