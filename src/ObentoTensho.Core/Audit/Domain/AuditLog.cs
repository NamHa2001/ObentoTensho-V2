using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Audit.Domain;

public sealed class AuditLog
{
    public long Id { get; private set; }

    public required AuditActorType ActorType { get; init; }

    public Guid? ActorId { get; init; }

    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public required string EntityId { get; init; }

    public string? OldDataJson { get; init; }

    public string? NewDataJson { get; init; }

    public string? IpAddress { get; init; }

    public required Guid CorrelationId { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
