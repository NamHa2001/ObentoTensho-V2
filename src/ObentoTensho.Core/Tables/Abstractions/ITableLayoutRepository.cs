using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Abstractions;

// Khu vực và bàn. Hàm "Find…" trả bản được theo dõi để sửa; "List…" trả bản chỉ đọc.
public interface ITableLayoutRepository
{
    Task<IReadOnlyList<TableArea>> ListAreasAsync(CancellationToken cancellationToken);

    Task<TableArea?> FindAreaAsync(int id, CancellationToken cancellationToken);

    Task<bool> AreaNameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken);

    Task<bool> AreaHasTablesAsync(int areaId, CancellationToken cancellationToken);

    void AddArea(TableArea area);

    void RemoveArea(TableArea area);

    Task<IReadOnlyList<DiningTable>> ListTablesAsync(int? areaId, CancellationToken cancellationToken);

    Task<DiningTable?> FindTableAsync(int id, CancellationToken cancellationToken);

    Task<bool> TableNameExistsAsync(int areaId, string name, int? exceptId, CancellationToken cancellationToken);

    void AddTable(DiningTable table);

    void RemoveTable(DiningTable table);
}
