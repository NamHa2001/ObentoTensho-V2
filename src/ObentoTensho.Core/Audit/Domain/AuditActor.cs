using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Audit.Domain;

public sealed record AuditActor(AuditActorType Type, Guid? Id)
{
    // Yêu cầu chưa xác thực (đăng nhập sai, nhận lời mời…) hoặc tác vụ nền
    public static readonly AuditActor System = new(AuditActorType.System, null);

    public static AuditActor Staff(Guid id) => new(AuditActorType.Staff, id);

    public static AuditActor User(Guid id) => new(AuditActorType.User, id);
}
