using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Validators;

// BR-MNU-19: giá cộng thêm từ 0 đến 10.000.000 ₫, số nguyên
public sealed class OptionRequestValidator : AbstractValidator<OptionRequest>
{
    public OptionRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText(100);
        RuleFor(r => r.PriceDelta).Money(0, MenuRules.MaxPriceDelta);
        RuleFor(r => r.Status).Status();
        RuleFor(r => r.Allergens).Allergens();
    }
}
