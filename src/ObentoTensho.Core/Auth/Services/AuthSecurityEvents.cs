using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Auth.Services;

// BR-STF-11: audit sự kiện xác thực. Hàm "Record…" chỉ thêm vào unit of work (lưu cùng thay đổi);
// hàm "…Async" tự lưu ngay vì nhánh thất bại không có thay đổi nào khác để lưu kèm.
public sealed class AuthSecurityEvents(IAuditTrail audit, IUnitOfWork unitOfWork)
{
    private const string StaffEntity = "Staff";
    private const string RefreshTokenFamilyEntity = "RefreshTokenFamily";

    // Email chưa tồn tại thì EntityId = "unknown"; email được che khi ghi
    public Task StaffLoginFailedAsync(Guid? staffId, string? email, string reason, CancellationToken cancellationToken)
    {
        audit.Record(new AuditEntry(
            AuditActions.StaffLoginFailed,
            StaffEntity,
            staffId?.ToString() ?? "unknown",
            After: new { email, reason },
            Actor: AuditActor.System));
        return unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task RefreshTokenReuseDetectedAsync(Guid familyId, Guid? userId, Guid? staffId, CancellationToken cancellationToken)
    {
        audit.Record(new AuditEntry(
            AuditActions.RefreshTokenReuseDetected,
            RefreshTokenFamilyEntity,
            familyId.ToString(),
            After: new { userId, staffId },
            Actor: AuditActor.System));
        return unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task StaffRecoveryCodeUsedAsync(Staff member, CancellationToken cancellationToken)
    {
        RecordStaff(AuditActions.StaffRecoveryCodeUsed, member);
        return unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public void RecordStaffLoginSucceeded(Staff member) =>
        RecordStaff(AuditActions.StaffLoginSucceeded, member, new { role = member.Role.ToString() });

    public void RecordStaffTwoFactorEnabled(Staff member) => RecordStaff(AuditActions.StaffTwoFactorEnabled, member);

    public void RecordStaffPasswordChanged(Staff member) => RecordStaff(AuditActions.StaffPasswordChanged, member);

    public void RecordStaffInviteAccepted(Staff member) => RecordStaff(AuditActions.StaffInviteAccepted, member);

    private void RecordStaff(string action, Staff member, object? after = null) =>
        audit.Record(new AuditEntry(action, StaffEntity, member.Id.ToString(), After: after, Actor: AuditActor.Staff(member.Id)));
}
