using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reports.Services;

// TIME-07, TIME-14: ngày kinh doanh VN bắt đầu từ `report.dayCutoffLocal`
public sealed class BusinessCalendar(IClock clock, ISettingsReader settings)
{
    public DateOnly Today
    {
        get
        {
            var local = clock.ToLocal(clock.UtcNow);
            return DateOnly.FromDateTime(local.Subtract(Cutoff.ToTimeSpan()));
        }
    }

    private TimeOnly Cutoff => TimeOnly.Parse(settings.Get(SettingKeys.ReportDayCutoffLocal), System.Globalization.CultureInfo.InvariantCulture);

    public (DateTime StartUtc, DateTime EndUtc) Bounds(DateOnly day) =>
        (clock.ToUtc(day, Cutoff), clock.ToUtc(day.AddDays(1), Cutoff));
}
