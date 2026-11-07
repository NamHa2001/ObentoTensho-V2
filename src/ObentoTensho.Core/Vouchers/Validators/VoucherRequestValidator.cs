using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Validators;

// BR-VCH-01: % là số nguyên 1..100, cố định > 0; trần giảm chỉ cho %; ngày kết thúc không trước ngày bắt đầu
public sealed class VoucherRequestValidator : AbstractValidator<VoucherRequest>
{
    public const decimal MaxAmount = 100_000_000m;
    public const int MaxUsage = 1_000_000;

    public VoucherRequestValidator()
    {
        RuleFor(r => r.Code)
            .Must(c => c is null || VoucherCodes.IsValid(VoucherCodes.Normalize(c)))
            .WithErrorCode(FieldErrorCodes.InvalidFormat);
        RuleFor(r => r.DiscountType).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.DiscountValue)
            .Must((r, v) => r.DiscountType == DiscountType.Percent
                ? v is >= 1 and <= 100 && decimal.Truncate(v) == v
                : v is > 0 and <= MaxAmount && decimal.Truncate(v) == v)
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxDiscountAmount)
            .Must((r, cap) => cap is null || (r.DiscountType == DiscountType.Percent && cap is > 0 and <= MaxAmount))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MinOrderValue).InclusiveBetween(0, MaxAmount).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxUsageTotal).Must(m => m is null or >= 1 and <= MaxUsage).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxUsagePerUser).InclusiveBetween(1, 100).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.ApplicableTier).Must(t => t is null || Enum.IsDefined(t.Value)).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.EndDate)
            .Must((r, end) => end is null || r.StartDate is null || end >= r.StartDate)
            .WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
