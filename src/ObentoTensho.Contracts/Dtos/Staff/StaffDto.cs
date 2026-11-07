using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Staff;

/// <summary><c>InvitePending</c>: chưa nhận lời mời, chưa có mật khẩu.</summary>
public sealed record StaffDto(
    Guid Id,
    string FullName,
    string Email,
    StaffRole Role,
    bool IsActive,
    bool TotpEnabled,
    bool InvitePending,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc);
