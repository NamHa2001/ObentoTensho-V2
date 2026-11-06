using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Infrastructure.Persistence;

internal sealed class EfAuditTrail(AppDbContext db, IAuditContext context, IClock clock) : IAuditTrail
{
    public void Record(AuditEntry entry)
    {
        var actor = entry.Actor ?? context.Actor;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = actor.Type,
            ActorId = actor.Id,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            OldDataJson = AuditRedactor.ToJson(entry.Before),
            NewDataJson = AuditRedactor.ToJson(entry.After),
            IpAddress = context.IpAddress,
            CorrelationId = context.CorrelationId,
            CreatedAtUtc = clock.UtcNow,
        });
    }
}
