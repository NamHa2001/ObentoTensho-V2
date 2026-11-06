namespace ObentoTensho.Core.Audit.Domain;

// Before/After là đối tượng bất kỳ; được chuyển JSON và che trường nhạy cảm khi ghi (BR-STF-10).
// Actor null ⇒ lấy người đang gọi API.
public sealed record AuditEntry(
    string Action,
    string EntityType,
    string EntityId,
    object? Before = null,
    object? After = null,
    AuditActor? Actor = null);
