using ObentoTensho.Core.Orders.Abstractions;

namespace ObentoTensho.Infrastructure.Orders;

// Giá trị mặc định theo docs/srs/19 §3 (`order.*`)
public sealed class OrderOptions : IOrderSettings
{
    public const string SectionName = "Order";

    public int MinLeadMinutes { get; set; } = 30;

    public int MaxLeadDays { get; set; } = 7;

    public int PickupStepMinutes { get; set; } = 15;

    public int MaxActivePerPhone { get; set; } = 3;

    public decimal CallConfirmThreshold { get; set; } = 500_000m;

    public int MaxLines { get; set; } = 50;

    public bool IsValid() =>
        MinLeadMinutes is >= 10 and <= 240 && MaxLeadDays is >= 1 and <= 30 && PickupStepMinutes is 5 or 10 or 15 or 30
        && MaxActivePerPhone is >= 1 and <= 10 && CallConfirmThreshold is >= 0 and <= 10_000_000m && MaxLines is >= 1 and <= 50;
}
