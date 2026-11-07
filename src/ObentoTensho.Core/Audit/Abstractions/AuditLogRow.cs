using ObentoTensho.Core.Audit.Domain;

namespace ObentoTensho.Core.Audit.Abstractions;

public sealed record AuditLogRow(AuditLog Log, string? ActorName);
