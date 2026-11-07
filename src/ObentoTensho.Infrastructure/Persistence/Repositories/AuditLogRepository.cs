using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Audit.Abstractions;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(AppDbContext db) : IAuditLogRepository
{
    public async Task<(IReadOnlyList<AuditLogRow> Items, int Total)> ListAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = db.AuditLogs.AsNoTracking()
            .Where(a => (query.ActorId == null || a.ActorId == query.ActorId)
                && (query.EntityType == null || a.EntityType == query.EntityType)
                && (query.EntityId == null || a.EntityId == query.EntityId)
                && (query.Action == null || a.Action == query.Action)
                && (query.FromUtc == null || a.CreatedAtUtc >= query.FromUtc)
                && (query.ToUtc == null || a.CreatedAtUtc < query.ToUtc));
        var total = await source.CountAsync(cancellationToken);
        var rows = await source
            .OrderByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new { Log = a, ActorName = db.StaffMembers.Where(s => s.Id == a.ActorId).Select(s => s.FullName).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        return (rows.Select(r => new AuditLogRow(r.Log, r.ActorName)).ToArray(), total);
    }
}
