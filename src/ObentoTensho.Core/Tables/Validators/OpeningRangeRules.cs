using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Validators;

public static class OpeningRangeRules
{
    // Ngày nghỉ không có khoảng; ngày mở có 1–4 khoảng, mỗi khoảng Mở < Đóng (hoặc qua nửa đêm: Đóng < Mở)
    public static void ValidRanges<T>(this IRuleBuilderInitial<T, IReadOnlyList<OpeningRangeDto>> rule, Func<T, bool> isClosed, bool allowCrossMidnight) =>
        rule
            .NotNull().WithErrorCode(FieldErrorCodes.Required)
            .Must((parent, ranges) => isClosed(parent) ? ranges is not { Count: > 0 } : ranges is { Count: > 0 and <= ScheduleRules.MaxRangesPerDay })
            .WithErrorCode(FieldErrorCodes.OutOfRange)
            .Must(ranges => ranges is null || ranges.All(r => IsValidRange(r, allowCrossMidnight)))
            .WithErrorCode(FieldErrorCodes.InvalidFormat);

    private static bool IsValidRange(OpeningRangeDto range, bool allowCrossMidnight) =>
        range is not null && (range.CrossesMidnight ? allowCrossMidnight && range.Close < range.Open : range.Open < range.Close);
}
