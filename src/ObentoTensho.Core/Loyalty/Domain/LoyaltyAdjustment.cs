using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Loyalty.Domain;

// BR-LOY-11/21: điều chỉnh điểm thủ công; vượt ngưỡng thì chờ người thứ hai duyệt
public sealed class LoyaltyAdjustment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required Guid UserId { get; init; }

    public required int Points { get; init; }

    public required string Reason { get; init; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public required Guid RequestedByStaffId { get; init; }

    public required DateTime RequestedAtUtc { get; init; }

    public Guid? ReviewedByStaffId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewNote { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public string ReferenceId => $"adjust:{Id:N}";
}
