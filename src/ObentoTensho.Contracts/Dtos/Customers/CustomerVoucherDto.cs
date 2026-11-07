using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Customers;

public sealed record CustomerVoucherDto(int Id, string Code, VoucherSource Source, bool IsActive, DateTime? EndUtc);
