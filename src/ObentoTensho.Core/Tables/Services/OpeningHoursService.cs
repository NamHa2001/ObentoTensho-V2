using System.Globalization;
using System.Buffers.Text;
using System.Security.Cryptography;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Tables.Services;

// Giờ mở cửa thường kỳ (chỉ SuperAdmin ghi) và giờ đặc biệt theo ngày (Manager+) — docs/srs/16
public sealed class OpeningHoursService(
    IScheduleRepository schedule,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IAuditContext actor,
    IPublicScheduleCache publicCache,
    IClock clock,
    IValidator<WeeklyHoursRequest> weeklyValidator,
    IValidator<SpecialHoursRequest> specialValidator)
{
    private const string WeeklyEntity = "opening_hours";
    private const string SpecialEntity = "special_hours";
    private const int PublicSpecialDays = 60;
    private const int SpecialListDays = 366;

    public async Task<WeeklyHoursDto> GetWeeklyAsync(CancellationToken cancellationToken)
    {
        var rows = await schedule.ListWeeklyAsync(forUpdate: false, cancellationToken);
        return new WeeklyHoursDto(rows.ToWeeklyDto(), WeeklyVersion(rows));
    }

    public async Task<Result<WeeklyHoursDto>> ReplaceWeeklyAsync(WeeklyHoursRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await weeklyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<WeeklyHoursDto>();
        }

        var current = await schedule.ListWeeklyAsync(forUpdate: true, cancellationToken);
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Result.Failure<WeeklyHoursDto>(ErrorCodes.PreconditionRequired);
        }

        if (!string.Equals(ifMatch.Trim().Trim('"'), WeeklyVersion(current), StringComparison.Ordinal))
        {
            return Result.Failure<WeeklyHoursDto>(ErrorCodes.PreconditionFailed);
        }

        var replacement = request.Days.SelectMany(ToRows).ToArray();

        // BR-TBL-03: không cho đổi giờ làm ca đang bật lọt ra ngoài giờ mở cửa
        var orphaned = (await schedule.ListSlotsAsync(cancellationToken))
            .Where(s => s.IsActive && !ScheduleRules.SlotFitsOpeningHours(s, replacement))
            .Select(s => s.Id.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        if (orphaned.Length > 0)
        {
            return Result.Failure<WeeklyHoursDto>(
                ErrorCodes.SlotOutsideOpeningHours,
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["serviceSlots"] = orphaned });
        }

        schedule.ReplaceWeekly(current, replacement);
        audit.Record(new AuditEntry(AuditActions.For(WeeklyEntity, AuditActions.Updated), WeeklyEntity, "weekly", current.ToWeeklyDto(), request.Days));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        publicCache.Invalidate();
        return Result.Success(await GetWeeklyAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<SpecialHoursDto>> ListSpecialAsync(CancellationToken cancellationToken)
    {
        var today = clock.TodayLocal;
        return (await schedule.ListSpecialAsync(today, today.AddDays(SpecialListDays), forUpdate: false, cancellationToken)).ToSpecialDto();
    }

    public async Task<Result<SpecialHoursDto>> SetSpecialAsync(DateOnly date, SpecialHoursRequest request, CancellationToken cancellationToken)
    {
        var validation = await specialValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<SpecialHoursDto>();
        }

        if (date < clock.TodayLocal || actor.Actor.Id is not { } staffId)
        {
            return Result.Failure<SpecialHoursDto>(ErrorCodes.ValidationFailed, DateError());
        }

        var current = await schedule.ListSpecialAsync(date, date, forUpdate: true, cancellationToken);
        var replacement = request.Closed
            ? [new SpecialHour { Date = date, IsClosed = true, Note = request.Note, CreatedByStaffId = staffId }]
            : request.Ranges.Select(r => new SpecialHour { Date = date, OpenTime = r.Open, CloseTime = r.Close, Note = request.Note, CreatedByStaffId = staffId }).ToArray();

        schedule.ReplaceSpecial(current, replacement);
        audit.Record(new AuditEntry(AuditActions.For(SpecialEntity, AuditActions.Updated), SpecialEntity, date.ToString("O", CultureInfo.InvariantCulture), current.ToSpecialDto(), replacement.ToSpecialDto()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        publicCache.Invalidate();
        return Result.Success(replacement.ToSpecialDto()[0]);
    }

    public async Task<Result> DeleteSpecialAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var current = await schedule.ListSpecialAsync(date, date, forUpdate: true, cancellationToken);
        if (current.Count == 0)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        schedule.ReplaceSpecial(current, []);
        audit.Record(new AuditEntry(AuditActions.For(SpecialEntity, AuditActions.Deleted), SpecialEntity, date.ToString("O", CultureInfo.InvariantCulture), Before: current.ToSpecialDto()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        publicCache.Invalidate();
        return Result.Success();
    }

    public Task<PublicOpeningHoursDto> GetPublicAsync(CancellationToken cancellationToken) =>
        publicCache.GetOrCreateAsync(async ct =>
        {
            var today = clock.TodayLocal;
            var weekly = await schedule.ListWeeklyAsync(forUpdate: false, ct);
            var special = await schedule.ListSpecialAsync(today, today.AddDays(PublicSpecialDays), forUpdate: false, ct);
            return new PublicOpeningHoursDto(weekly.ToWeeklyDto(), special.ToSpecialDto());
        }, cancellationToken);

    // ETag của cả lịch tuần: đổi khi bất kỳ dòng nào đổi (thay toàn bộ ⇒ Id mới)
    private static string WeeklyVersion(IEnumerable<OpeningHour> rows)
    {
        var material = string.Join('|', rows.OrderBy(r => r.Id).Select(r => $"{r.Id}:{Convert.ToHexString(r.RowVersion)}"));
        return Base64Url.EncodeToString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material)).AsSpan(0, 12));
    }

    private static IEnumerable<OpeningHour> ToRows(OpeningDayDto day) =>
        day.Closed
            ? [new OpeningHour { DayOfWeek = (byte)day.DayOfWeek, OpenTime = TimeOnly.MinValue, CloseTime = TimeOnly.MinValue, IsClosed = true }]
            : day.Ranges.Select(r => new OpeningHour { DayOfWeek = (byte)day.DayOfWeek, OpenTime = r.Open, CloseTime = r.Close, CrossesMidnight = r.CrossesMidnight });

    private static Dictionary<string, string[]> DateError() =>
        new(StringComparer.Ordinal) { ["date"] = [FieldErrorCodes.OutOfRange] };
}
