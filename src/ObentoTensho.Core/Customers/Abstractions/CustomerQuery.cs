using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Customers.Abstractions;

// ToUtc là mốc loại trừ
public sealed record CustomerQuery(string? Search, MemberTier? Tier, bool? IsActive, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize);
