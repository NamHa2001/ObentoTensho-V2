using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Vouchers.Validators;

// Mức giảm theo BR-VCH-01 như voucher thường; điểm và số ngày hiệu lực trong khoảng hợp lý
public sealed class RedeemRewardRequestValidator : AbstractValidator<RedeemRewardRequest>
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;
    public const int MaxPoints = 1_000_000;
    public const int MaxValidDays = 365;

    public RedeemRewardRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithErrorCode(FieldErrorCodes.Required)
            .MaximumLength(NameMaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .NoMarkup();
        RuleFor(r => r.Description!).MaximumLength(DescriptionMaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .NoMarkup();
        RuleFor(r => r.PointsCost).InclusiveBetween(1, MaxPoints).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.DiscountType).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.DiscountValue)
            .Must((r, v) => r.DiscountType == DiscountType.Percent
                ? v is >= 1 and <= 100 && decimal.Truncate(v) == v
                : v is > 0 and <= VoucherRequestValidator.MaxAmount && decimal.Truncate(v) == v)
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxDiscountAmount)
            .Must((r, cap) => cap is null || (r.DiscountType == DiscountType.Percent && cap is > 0 and <= VoucherRequestValidator.MaxAmount))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MinOrderValue).InclusiveBetween(0, VoucherRequestValidator.MaxAmount).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.ValidDays).InclusiveBetween(1, MaxValidDays).WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
