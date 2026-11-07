using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Content.Abstractions;

// Những gì cần biết về đơn để quyết định có được đánh giá không
public sealed record ReviewOrderFacts(OrderStatus Status, DateTime? CompletedAtUtc, IReadOnlyList<int> MenuItemIds);
