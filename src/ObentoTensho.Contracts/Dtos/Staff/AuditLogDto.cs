using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Staff;

public sealed record AuditLogDto(
    long Id,
    AuditActorType ActorType,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    string EntityId,
    string? OldDataJson,
    string? NewDataJson,
    string? IpAddress,
    Guid CorrelationId,
    DateTime CreatedAtUtc);
