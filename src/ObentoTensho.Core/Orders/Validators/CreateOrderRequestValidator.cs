using FluentValidation;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Reservations.Validators;

namespace ObentoTensho.Core.Orders.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(IOrderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RuleFor(r => r.CustomerName).GuestName();
        RuleFor(r => r.CustomerPhone).GuestPhone();
        RuleFor(r => r.CustomerEmail).ValidEmail();
        RuleFor(r => r.PaymentMethod).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Items).Lines(settings.MaxLines);
        RuleFor(r => r.Note).PlainText();
        RuleFor(r => r.Lang).Language();
    }
}
