using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Tables.Validators;

namespace ObentoTensho.UnitTests.Tables;

// docs/srs/16 §8: tiêu chí nghiệm thu về giờ mở cửa, ca, khóa ngày
public sealed class ScheduleRulesTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static OpeningHour Hour(int day, string open, string close, bool crosses = false) => new()
    {
        DayOfWeek = (byte)day,
        OpenTime = TimeOnly.Parse(open, System.Globalization.CultureInfo.InvariantCulture),
        CloseTime = TimeOnly.Parse(close, System.Globalization.CultureInfo.InvariantCulture),
        CrossesMidnight = crosses,
    };

    private static ServiceSlot Slot(int? day, string start, string end) => new()
    {
        DayOfWeek = (byte?)day,
        StartTime = TimeOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
        EndTime = TimeOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture),
        MaxReservations = 10,
    };

    // Mỗi ngày 10:30–14:00 và 17:00–21:00
    private static OpeningHour[] TwoShiftWeek() =>
        Enumerable.Range(0, 7).SelectMany(d => new[] { Hour(d, "10:30", "14:00"), Hour(d, "17:00", "21:00") }).ToArray();

    [Fact]
    public void Ca_10_den_15_khi_mo_10h30_14h_va_17h_21h_bi_tu_choi()
    {
        Assert.False(ScheduleRules.SlotFitsOpeningHours(Slot(null, "10:00", "15:00"), TwoShiftWeek()));
        Assert.True(ScheduleRules.SlotFitsOpeningHours(Slot(null, "11:00", "13:00"), TwoShiftWeek()));
        Assert.True(ScheduleRules.SlotFitsOpeningHours(Slot(1, "17:00", "21:00"), TwoShiftWeek()));
    }

    [Fact]
    public void Ca_cua_ngay_nghi_bi_tu_choi_ca_moi_ngay_bo_qua_ngay_nghi()
    {
        var closedMonday = TwoShiftWeek().Where(h => h.DayOfWeek != 1).Append(new OpeningHour
        {
            DayOfWeek = 1, OpenTime = TimeOnly.MinValue, CloseTime = TimeOnly.MinValue, IsClosed = true,
        }).ToArray();

        Assert.False(ScheduleRules.SlotFitsOpeningHours(Slot(1, "11:00", "13:00"), closedMonday));
        Assert.True(ScheduleRules.SlotFitsOpeningHours(Slot(null, "11:00", "13:00"), closedMonday));
    }

    [Fact]
    public void Khoang_qua_nua_dem_tinh_vao_dau_ngay_hom_sau_va_bien_dong_la_mo()
    {
        var schedule = new OpeningSchedule([Hour(5, "18:00", "02:00", crosses: true)], []);
        var saturday = new DateOnly(2026, 11, 14);

        Assert.True(schedule.IsOpen(saturday, new TimeOnly(0, 30), new TimeOnly(1, 30)));
        Assert.False(schedule.IsOpen(saturday, new TimeOnly(1, 30), new TimeOnly(2, 30)));
        Assert.True(schedule.IsOpen(saturday.AddDays(-1), new TimeOnly(23, 0), new TimeOnly(1, 0)));
    }

    [Fact]
    public void Gio_dac_biet_ghi_de_lich_tuan()
    {
        var holiday = Monday.AddDays(1);
        var schedule = new OpeningSchedule(TwoShiftWeek(), [new SpecialHour { Date = holiday, IsClosed = true, CreatedByStaffId = Guid.Empty }]);

        Assert.Empty(schedule.IntervalsStartingOn(holiday));
        Assert.Equal(2, schedule.IntervalsStartingOn(Monday).Count);
    }

    [Fact]
    public void Khoa_12h_14h_thi_13h_bi_chan_14h_duoc_nhan_va_dung_pham_vi()
    {
        var block = new BlockedDate
        {
            Date = Monday, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(14, 0),
            AppliesTo = BlockScope.Reservation, CreatedByStaffId = Guid.Empty, CreatedAtUtc = DateTime.UnixEpoch,
        };

        Assert.True(block.Covers(Monday, new TimeOnly(13, 0), BlockScope.Reservation));
        Assert.False(block.Covers(Monday, new TimeOnly(14, 0), BlockScope.Reservation));
        Assert.False(block.Covers(Monday, new TimeOnly(13, 0), BlockScope.Order));
        Assert.False(block.Covers(Monday.AddDays(1), new TimeOnly(13, 0), BlockScope.Reservation));
    }

    [Fact]
    public void Ca_moi_ngay_chong_lan_ca_cua_mot_ngay_cu_the()
    {
        Assert.True(ScheduleRules.SlotsOverlap(Slot(null, "11:00", "13:00"), Slot(3, "12:00", "14:00")));
        Assert.False(ScheduleRules.SlotsOverlap(Slot(2, "11:00", "13:00"), Slot(3, "12:00", "14:00")));
        Assert.False(ScheduleRules.SlotsOverlap(Slot(null, "11:00", "13:00"), Slot(null, "13:00", "14:00")));
    }

    [Fact]
    public void Lich_tuan_chong_lan_qua_nua_dem_bi_tu_choi()
    {
        var days = Enumerable.Range(0, 7).Select(d => new OpeningDayDto(d, true, [])).ToList();
        days[6] = new OpeningDayDto(6, false, [new OpeningRangeDto(new TimeOnly(20, 0), new TimeOnly(2, 0), true)]);
        days[0] = new OpeningDayDto(0, false, [new OpeningRangeDto(new TimeOnly(1, 0), new TimeOnly(10, 0), false)]);

        var result = new WeeklyHoursRequestValidator().Validate(new WeeklyHoursRequest(days));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => string.Equals(e.ErrorCode, "OVERLAP", StringComparison.Ordinal));
    }

    [Fact]
    public void Lich_tuan_thieu_ngay_hoac_ngay_nghi_co_khoang_gio_bi_tu_choi()
    {
        var validator = new WeeklyHoursRequestValidator();
        var sixDays = Enumerable.Range(0, 6).Select(d => new OpeningDayDto(d, true, [])).ToArray();
        var closedWithRange = Enumerable.Range(0, 7)
            .Select(d => new OpeningDayDto(d, true, d == 0 ? [new OpeningRangeDto(new TimeOnly(9, 0), new TimeOnly(10, 0), false)] : []))
            .ToArray();

        Assert.False(validator.Validate(new WeeklyHoursRequest(sixDays)).IsValid);
        Assert.False(validator.Validate(new WeeklyHoursRequest(closedWithRange)).IsValid);
    }
}
