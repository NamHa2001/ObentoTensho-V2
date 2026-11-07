namespace ObentoTensho.Infrastructure.Orders;

// Thông số kỹ thuật ngoài danh mục cài đặt
public sealed class OrderOptions
{
    public const string SectionName = "Order";

    public int PickupStepMinutes { get; set; } = 15;

    public int MaxLines { get; set; } = 50;

    public int PendingAlertMinutes { get; set; } = 30;

    public bool IsValid() => PickupStepMinutes is 5 or 10 or 15 or 30 && MaxLines is >= 1 and <= 50 && PendingAlertMinutes is >= 5 and <= 240;
}
