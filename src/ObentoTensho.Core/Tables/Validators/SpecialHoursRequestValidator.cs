using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Validators;

// Giờ đặc biệt chỉ trong ngày đó (không qua nửa đêm), các khoảng không chồng lấn
public sealed class SpecialHoursRequestValidator : AbstractValidator<SpecialHoursRequest>
{
    public const int NoteMaxLength = 200;

    public SpecialHoursRequestValidator()
    {
        RuleFor(r => r.Ranges).ValidRanges(r => r.Closed, allowCrossMidnight: false);
        RuleFor(r => r.Ranges)
            .Must(ranges => !ScheduleRules.RangesOverlap(ranges))
            .When(r => r.Ranges is not null && r.Ranges.All(x => x is not null))
            .WithErrorCode(FieldErrorCodes.Overlap);
        RuleFor(r => r.Note)
            .Must(n => n is null || n.Length <= NoteMaxLength).WithErrorCode(FieldErrorCodes.TooLong);
    }
}
