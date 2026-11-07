using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Customers.Abstractions;

// Một loại dữ liệu xuất được
public interface IDataExportSource
{
    DataExportKind Kind { get; }

    string SheetName { get; }

    string FilePrefix { get; }

    IReadOnlyList<string> Columns { get; }

    Task<IReadOnlyList<IReadOnlyList<string>>> LoadRowsAsync(CancellationToken cancellationToken);
}
