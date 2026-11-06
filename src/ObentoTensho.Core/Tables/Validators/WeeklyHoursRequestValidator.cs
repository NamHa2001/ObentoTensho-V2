using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Validators;

// BR-TBL-02, BR-TBL-12: đủ 7 ngày, mỗi ngày một lần; các khoảng không chồng lấn (kể cả qua nửa đêm)
public sealed class WeeklyHoursRequestValidator : AbstractValidator<WeeklyHoursRequest>
{
    public WeeklyHoursRequestValidator()
    {
        RuleFor(r => r.Days)
            .NotNull().WithErrorCode(FieldErrorCodes.Required)
            .Must(days => days is not null && days.Count == 7 && days.Select(d => d?.DayOfWeek).Distinct().Count() == 7
                && days.All(d => d is { DayOfWeek: >= 0 and <= 6 }))
            .WithErrorCode(FieldErrorCodes.OutOfRange);

        RuleForEach(r => r.Days).ChildRules(day =>
            day.RuleFor(d => d.Ranges).ValidRanges(d => d.Closed, allowCrossMidnight: true));

        RuleFor(r => r.Days)
            .Must(days => !ScheduleRules.WeeklyRangesOverlap(days))
            .When((r, _) => r.Days is { Count: 7 } && r.Days.All(d => d?.Ranges is not null))
            .WithErrorCode(FieldErrorCodes.Overlap);
    }
}
