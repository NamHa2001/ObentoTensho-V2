using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Staff;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Audit.Services;

// FR-STF-05: chỉ SuperAdmin xem, lọc theo người, đối tượng, ngày VN
public sealed class AuditLogService(IAuditLogRepository logs, IClock clock)
{
    public const int MaxPageSize = 100;

    public async Task<PagedResponse<AuditLogDto>> ListAsync(
        Guid? actorId,
        string? entityType,
        string? entityId,
        string? action,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var fromUtc = from is { } start ? clock.ToUtc(start, TimeOnly.MinValue) : (DateTime?)null;
        var toUtc = to is { } end ? clock.ToUtc(end.AddDays(1), TimeOnly.MinValue) : (DateTime?)null;
        var (items, total) = await logs.ListAsync(new AuditLogQuery(actorId, entityType, entityId, action, fromUtc, toUtc, page, pageSize), cancellationToken);
        return new PagedResponse<AuditLogDto>(
            items.Select(row => new AuditLogDto(
                row.Log.Id,
                row.Log.ActorType,
                row.Log.ActorId,
                row.ActorName,
                row.Log.Action,
                row.Log.EntityType,
                row.Log.EntityId,
                row.Log.OldDataJson,
                row.Log.NewDataJson,
                row.Log.IpAddress,
                row.Log.CorrelationId,
                row.Log.CreatedAtUtc)).ToArray(),
            new PageMeta(page, pageSize, total));
    }
}
