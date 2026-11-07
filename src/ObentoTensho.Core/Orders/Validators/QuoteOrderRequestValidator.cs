using FluentValidation;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Core.Orders.Abstractions;

namespace ObentoTensho.Core.Orders.Validators;

public sealed class QuoteOrderRequestValidator : AbstractValidator<QuoteOrderRequest>
{
    public QuoteOrderRequestValidator(IOrderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RuleFor(r => r.Items).Lines(settings.MaxLines);
        RuleFor(r => r.Lang).Language();
    }
}
