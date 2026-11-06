using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class TableLayoutRepository(AppDbContext db) : ITableLayoutRepository
{
    public async Task<IReadOnlyList<TableArea>> ListAreasAsync(CancellationToken cancellationToken) =>
        await db.TableAreas.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);

    public Task<TableArea?> FindAreaAsync(int id, CancellationToken cancellationToken) =>
        db.TableAreas.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> AreaNameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken) =>
        db.TableAreas.AnyAsync(a => a.Name == name && a.Id != exceptId, cancellationToken);

    public Task<bool> AreaHasTablesAsync(int areaId, CancellationToken cancellationToken) =>
        db.Tables.AnyAsync(t => t.AreaId == areaId, cancellationToken);

    public void AddArea(TableArea area) => db.TableAreas.Add(area);

    public void RemoveArea(TableArea area) => db.TableAreas.Remove(area);

    public async Task<IReadOnlyList<DiningTable>> ListTablesAsync(int? areaId, CancellationToken cancellationToken) =>
        await db.Tables.AsNoTracking()
            .Where(t => areaId == null || t.AreaId == areaId)
            .OrderBy(t => t.AreaId).ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public Task<DiningTable?> FindTableAsync(int id, CancellationToken cancellationToken) =>
        db.Tables.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> TableNameExistsAsync(int areaId, string name, int? exceptId, CancellationToken cancellationToken) =>
        db.Tables.AnyAsync(t => t.AreaId == areaId && t.Name == name && t.Id != exceptId, cancellationToken);

    public void AddTable(DiningTable table) => db.Tables.Add(table);

    public void RemoveTable(DiningTable table) => db.Tables.Remove(table);
}
