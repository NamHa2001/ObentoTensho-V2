using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Orders.Domain;

// Mỗi lần chuyển trạng thái ghi ai, khi nào, từ → sang, lý do; FromStatus null = lúc tạo
public sealed class OrderStatusHistory
{
    public long Id { get; private set; }

    public required Guid OrderId { get; init; }

    public OrderStatus? FromStatus { get; init; }

    public required OrderStatus ToStatus { get; init; }

    public required OrderActor ChangedByType { get; init; }

    public Guid? ChangedById { get; init; }

    public string? Reason { get; init; }

    public required DateTime ChangedAtUtc { get; init; }
}
