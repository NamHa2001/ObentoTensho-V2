using Microsoft.Extensions.Options;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Infrastructure.Orders;
using static ObentoTensho.Core.Settings.Domain.SettingKeys;

namespace ObentoTensho.Infrastructure.Settings;

internal sealed class StoreOrderSettings(ISettingsReader s, IOptions<OrderOptions> options) : IOrderSettings
{
    public int MinLeadMinutes => s.GetInt(OrderMinLeadMinutes);

    public int MaxLeadDays => s.GetInt(OrderMaxLeadDays);

    public int PickupStepMinutes => options.Value.PickupStepMinutes;

    public int MaxActivePerPhone => s.GetInt(OrderMaxActivePerPhone);

    public decimal CallConfirmThreshold => s.GetInt(OrderCallConfirmThreshold);

    public int MaxLines => options.Value.MaxLines;

    public int NoPickupMinutes => s.GetInt(OrderNoPickupMinutes);

    public int NoPickupFlagCount => s.GetInt(OrderNoPickupFlagCount);

    public int PendingAlertMinutes => options.Value.PendingAlertMinutes;
}
