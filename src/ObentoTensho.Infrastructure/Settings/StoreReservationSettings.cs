using Microsoft.Extensions.Options;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Infrastructure.Reservations;
using static ObentoTensho.Core.Settings.Domain.SettingKeys;

namespace ObentoTensho.Infrastructure.Settings;

// docs/srs/19 §3.1: khóa trong danh mục đọc từ bảng cài đặt; thông số kỹ thuật ngoài danh mục vẫn ở cấu hình
internal sealed class StoreReservationSettings(ISettingsReader s, IOptions<ReservationOptions> options) : IReservationSettings
{
    public int MaxGuestsPerBooking => s.GetInt(ReservationMaxGuests);

    public int MaxAdvanceDays => s.GetInt(ReservationMaxAdvanceDays);

    public int MaxActivePerPhone => s.GetInt(ReservationMaxActivePerPhone);

    public int CancellationHours => s.GetInt(ReservationCancellationHours);

    public bool RequireContactVerify => s.GetBool(ReservationRequireContactVerify);

    public int CallAttempts => s.GetInt(ReservationCallAttempts);

    public int NoShowFlagCount => s.GetInt(ReservationNoShowFlagCount);

    public int LargePartySize => s.GetInt(ReservationLargePartySize);

    public int TimeStepMinutes => options.Value.TimeStepMinutes;

    public int GraceMinutes => s.GetInt(ReservationGraceMinutes);

    public int PendingCutoffMinutes => s.GetInt(ReservationPendingCutoffMinutes);

    public int PendingAlertMinutes => s.GetInt(ReservationPendingAlertMinutes);

    public int ReminderLeadMinutes => options.Value.ReminderLeadMinutes;
}
