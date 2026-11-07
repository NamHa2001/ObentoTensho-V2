using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reports;

public sealed record OrderStatusCountDto(OrderStatus Status, int Count);
