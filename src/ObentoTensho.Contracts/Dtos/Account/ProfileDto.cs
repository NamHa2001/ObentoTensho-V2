using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Account;

/// <summary><c>DeletionScheduledAtUtc</c> khác null: tài khoản sẽ bị ẩn danh tại mốc đó, còn hủy được.</summary>
public sealed record ProfileDto(
    string FullName,
    string Email,
    string? PendingEmail,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string PreferredLang,
    bool IsEmailVerified,
    MemberTier MemberTier,
    int CurrentPoints,
    DateTime? DeletionScheduledAtUtc);
