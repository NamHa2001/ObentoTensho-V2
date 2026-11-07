using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Customers.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class DataExportRepository(AppDbContext db) : IDataExportRepository
{
    public Task<int> CountSinceAsync(Guid staffId, DataExportKind kind, DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.DataExports.CountAsync(e => e.RequestedByStaffId == staffId && e.Kind == kind && e.CreatedAtUtc >= sinceUtc, cancellationToken);

    public async Task<IReadOnlyList<DataExportRow>> ListForStaffAsync(Guid staffId, int limit, CancellationToken cancellationToken) =>
        await db.DataExports.AsNoTracking()
            .Where(e => e.RequestedByStaffId == staffId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(limit)
            .Select(e => new DataExportRow(e.Id, e.Kind, e.Status, e.RowCount, e.CreatedAtUtc, e.ExpiresAtUtc))
            .ToListAsync(cancellationToken);

    public Task<DataExport?> FindNextQueuedAsync(CancellationToken cancellationToken) =>
        db.DataExports.Where(e => e.Status == DataExportStatus.Queued).OrderBy(e => e.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

    public Task<DataExport?> FindByTokenHashAsync(string hash, CancellationToken cancellationToken) =>
        db.DataExports.FirstOrDefaultAsync(e => e.DownloadTokenHash == hash, cancellationToken);

    public Task<int> ExpireAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
        db.DataExports
            .Where(e => e.Status == DataExportStatus.Ready && e.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.Status, DataExportStatus.Expired).SetProperty(e => e.Content, (byte[]?)null).SetProperty(e => e.DownloadTokenHash, (string?)null),
                cancellationToken);

    public void Add(DataExport export) => db.DataExports.Add(export);
}
