using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Validators;

namespace ObentoTensho.Core.Vouchers.Validators;

public sealed class VoucherOptionsRequestValidator : AbstractValidator<VoucherOptionsRequest>
{
    public VoucherOptionsRequestValidator(IOrderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RuleFor(r => r.Items).Lines(settings.MaxLines);
    }
}
