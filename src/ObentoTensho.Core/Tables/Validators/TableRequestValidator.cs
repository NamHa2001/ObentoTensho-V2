using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Tables.Validators;

// BR-TBL-05: 1 ≤ MinCapacity ≤ MaxCapacity; vị trí trong giới hạn sơ đồ
public sealed class TableRequestValidator : AbstractValidator<TableRequest>
{
    public const int NameMaxLength = 50;
    public const int MaxSeats = 100;
    public const int CanvasLimit = 10_000;
    public const int SizeLimit = 1_000;

    public TableRequestValidator()
    {
        RuleFor(r => r.AreaId).GreaterThan(0).WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(FieldErrorCodes.Required)
            .Must(n => n is null || n.Trim().Length <= NameMaxLength).WithErrorCode(FieldErrorCodes.TooLong);
        RuleFor(r => r.MinCapacity).InclusiveBetween(1, MaxSeats).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxCapacity)
            .Must((r, max) => max >= r.MinCapacity && max <= MaxSeats).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Layout).NotNull().WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.Layout)
            .Must(l => l.X is >= 0 and <= CanvasLimit && l.Y is >= 0 and <= CanvasLimit
                && l.Width is >= 1 and <= SizeLimit && l.Height is >= 1 and <= SizeLimit
                && l.Rotation is >= 0 and < 360)
            .When(r => r.Layout is not null)
            .WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
