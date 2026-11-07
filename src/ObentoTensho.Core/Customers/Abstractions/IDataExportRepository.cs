using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Customers.Domain;

namespace ObentoTensho.Core.Customers.Abstractions;

public interface IDataExportRepository
{
    Task<int> CountSinceAsync(Guid staffId, DataExportKind kind, DateTime sinceUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<DataExportRow>> ListForStaffAsync(Guid staffId, int limit, CancellationToken cancellationToken);

    Task<DataExport?> FindNextQueuedAsync(CancellationToken cancellationToken);

    Task<DataExport?> FindByTokenHashAsync(string hash, CancellationToken cancellationToken);

    // Xóa tệp đã hết hạn, giữ dòng; trả số tệp đã xóa
    Task<int> ExpireAsync(DateTime nowUtc, CancellationToken cancellationToken);

    void Add(DataExport export);
}
