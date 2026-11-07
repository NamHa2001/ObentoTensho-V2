namespace ObentoTensho.Core.Audit.Abstractions;

// BR-STF-13: chỉ đọc; không có thao tác sửa/xóa
public interface IAuditLogRepository
{
    Task<(IReadOnlyList<AuditLogRow> Items, int Total)> ListAsync(AuditLogQuery query, CancellationToken cancellationToken);
}
