using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Validators;

// BR-VCH-10: tối đa 5.000 mã một lần; tiền tố chỉ chữ/số
public sealed class BulkGenerateVouchersRequestValidator : AbstractValidator<BulkGenerateVouchersRequest>
{
    public const int MaxQuantity = 5000;

    public BulkGenerateVouchersRequestValidator()
    {
        RuleFor(r => r.Quantity).InclusiveBetween(1, MaxQuantity).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Prefix)
            .Must(p => p is null || (VoucherCodes.Normalize(p) is { Length: >= 1 and <= VoucherCodes.MaxPrefixLength } n && n.All(char.IsAsciiLetterOrDigit)))
            .WithErrorCode(FieldErrorCodes.InvalidFormat);
        RuleFor(r => r.Template).NotNull().WithErrorCode(FieldErrorCodes.Required).SetValidator(new VoucherRequestValidator());
    }
}
