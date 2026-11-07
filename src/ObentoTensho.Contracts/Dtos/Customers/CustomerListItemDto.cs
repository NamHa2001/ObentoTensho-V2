using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Customers;

/// <summary>Danh sách chỉ có email/SĐT đã che; xem đầy đủ ở chi tiết, có ghi audit (BR-CUS-07).</summary>
public sealed record CustomerListItemDto(
    Guid Id,
    string FullName,
    string EmailMasked,
    string? PhoneMasked,
    MemberTier MemberTier,
    int CurrentPoints,
    bool IsActive,
    bool IsEmailVerified,
    DateTime CreatedAtUtc);
