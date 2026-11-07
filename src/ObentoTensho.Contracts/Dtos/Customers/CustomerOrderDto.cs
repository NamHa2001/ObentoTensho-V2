using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Customers;

public sealed record CustomerOrderDto(Guid Id, string OrderCode, decimal TotalAmount, OrderStatus Status, DateTime CreatedAtUtc);
