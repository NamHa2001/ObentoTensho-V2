namespace ObentoTensho.Core.Audit.Abstractions;

// BR-STF-14: mốc thời gian đã đổi sang UTC theo ngày VN; ToUtc là mốc loại trừ
public sealed record AuditLogQuery(Guid? ActorId, string? EntityType, string? EntityId, string? Action, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize);
