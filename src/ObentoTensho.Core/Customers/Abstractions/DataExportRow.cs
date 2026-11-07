using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Customers.Abstractions;

// Không kèm nội dung tệp
public sealed record DataExportRow(Guid Id, DataExportKind Kind, DataExportStatus Status, int RowCount, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc);
