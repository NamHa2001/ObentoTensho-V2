using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Loyalty;

/// <summary>Đề nghị điều chỉnh điểm; <c>Status = Approved</c> nghĩa là đã ghi vào sổ cái.</summary>
public sealed record LoyaltyAdjustmentDto(
    Guid Id,
    Guid UserId,
    int Points,
    string Reason,
    ApprovalStatus Status,
    Guid RequestedByStaffId,
    DateTime RequestedAtUtc,
    Guid? ReviewedByStaffId,
    DateTime? ReviewedAtUtc,
    string? ReviewNote);
