using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Customers;

public sealed record DataExportDto(Guid Id, DataExportKind Kind, DataExportStatus Status, int RowCount, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc);
