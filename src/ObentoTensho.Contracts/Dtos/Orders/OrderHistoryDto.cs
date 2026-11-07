using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Orders;

public sealed record OrderHistoryDto(
    OrderStatus? FromStatus,
    OrderStatus ToStatus,
    OrderActor ChangedBy,
    Guid? ChangedById,
    string? Reason,
    DateTime ChangedAtUtc);
