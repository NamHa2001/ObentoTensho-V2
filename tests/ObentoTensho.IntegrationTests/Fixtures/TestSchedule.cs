using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Lịch mở cửa/ca là dữ liệu dùng chung của CSDL test: mỗi test cần lịch thì đặt lại từ đầu.
// Xóa đặt bàn trước vì Reservations tham chiếu ServiceSlots (không xóa dây chuyền).
public static class TestSchedule
{
    public static DateOnly TodayVn => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    public static DateOnly Tomorrow => TodayVn.AddDays(1);

    public static async Task ResetToSeedAsync(SqlServerFixture fixture)
    {
        await using var db = fixture.CreateContext();
        await ClearAsync(db);
        db.OpeningHours.AddRange(Enumerable.Range(0, 7).Select(d => new OpeningHour
        {
            DayOfWeek = (byte)d, OpenTime = TimeOnly.MinValue, CloseTime = TimeOnly.MinValue, IsClosed = true,
        }));
        await db.SaveChangesAsync();
    }

    // Mở 06:00–23:00 mọi ngày, một ca 10:00–14:00 mọi ngày; trả Id ca
    public static async Task<int> OpenEveryDayAsync(SqlServerFixture fixture, int maxReservations = 10, int? maxGuests = null)
    {
        await using var db = fixture.CreateContext();
        await ClearAsync(db);
        db.OpeningHours.AddRange(Enumerable.Range(0, 7).Select(d => new OpeningHour
        {
            DayOfWeek = (byte)d, OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(23, 0),
        }));
        var slot = new ServiceSlot
        {
            StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(14, 0), MaxReservations = maxReservations, MaxGuests = maxGuests,
        };
        db.ServiceSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot.Id;
    }

    private static async Task ClearAsync(Infrastructure.Persistence.AppDbContext db)
    {
        await db.Reservations.ExecuteDeleteAsync();
        await db.SlotDayCapacity.ExecuteDeleteAsync();
        await db.ServiceSlots.ExecuteDeleteAsync();
        await db.SpecialHours.ExecuteDeleteAsync();
        await db.BlockedDates.ExecuteDeleteAsync();
        await db.OpeningHours.ExecuteDeleteAsync();
    }
}
