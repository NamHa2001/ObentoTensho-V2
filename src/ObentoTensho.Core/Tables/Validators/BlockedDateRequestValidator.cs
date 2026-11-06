using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Tables.Validators;

// BR-TBL-04: cả ngày (không giờ) hoặc khung [Start, End); không khóa ngày đã qua (theo giờ VN)
public sealed class BlockedDateRequestValidator : AbstractValidator<BlockedDateRequest>
{
    public const int ReasonMaxLength = 300;

    public BlockedDateRequestValidator(IClock clock)
    {
        RuleFor(r => r.Date).Must(d => d >= clock.TodayLocal).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.AppliesTo).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.End)
            .Must((r, end) => (r.Start is null && end is null) || (r.Start is { } start && end is { } e && start < e))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Reason).Must(n => n is null || n.Length <= ReasonMaxLength).WithErrorCode(FieldErrorCodes.TooLong);
    }
}
