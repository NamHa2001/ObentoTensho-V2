namespace ObentoTensho.Contracts.Dtos.Invoices;

/// <summary>Số CCCD và SĐT đầy đủ của người mua (mỗi lần xem đều ghi audit).</summary>
public sealed record InvoiceContactDto(string? NationalId, string? Phone);
