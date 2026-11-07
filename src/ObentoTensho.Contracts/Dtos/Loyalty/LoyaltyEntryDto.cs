using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Loyalty;

/// <summary>Một dòng sổ cái điểm: (+) tích, (−) dùng/hết hạn/thu hồi; <c>ReferenceId</c> là mã đơn/đặt bàn khi có.</summary>
public sealed record LoyaltyEntryDto(long Id, int Points, int BalanceAfter, LoyaltyTransactionType Type, string? ReferenceId, string? Note, DateTime CreatedAtUtc);
