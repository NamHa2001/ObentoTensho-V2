using FluentValidation;
using ObentoTensho.Contracts.Dtos.Loyalty;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Loyalty.Validators;

// BR-LOY-11: khác 0, có trần để tránh gõ nhầm số khổng lồ; lý do bắt buộc
public sealed class AdjustPointsRequestValidator : AbstractValidator<AdjustPointsRequest>
{
    public const int MaxPoints = 100_000;
    public const int ReasonMaxLength = 300;

    public AdjustPointsRequestValidator()
    {
        RuleFor(r => r.Points).Must(p => p != 0 && Math.Abs(p) <= MaxPoints).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithErrorCode(FieldErrorCodes.Required)
            .Must(r => r is null || r.Trim().Length <= ReasonMaxLength).WithErrorCode(FieldErrorCodes.TooLong);
    }
}
