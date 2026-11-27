using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Orders.Services;

// Chủ quán 2026-11-27: đơn quầy/app đã trả tiền là hoàn tất; chuyển các đơn cũ còn kẹt ở trạng thái mở
public sealed class PaidBillCompletionJob(DineInService dineIn) : IRecurringJob
{
    public string Name => "orders.complete-paid-bills";

    public TimeSpan Interval => TimeSpan.FromMinutes(10);

    public Task<int> RunAsync(CancellationToken cancellationToken) => dineIn.CompletePaidBillsAsync(cancellationToken);
}
