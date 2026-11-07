using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Validators;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Validators;

public sealed class ValidateVoucherRequestValidator : AbstractValidator<ValidateVoucherRequest>
{
    public ValidateVoucherRequestValidator(IOrderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RuleFor(r => r.Code).Must(c => !string.IsNullOrWhiteSpace(c) && c.Trim().Length <= VoucherCodes.MaxLength).WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.Items).Lines(settings.MaxLines);
    }
}
