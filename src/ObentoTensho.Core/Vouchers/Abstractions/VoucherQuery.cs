using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Vouchers.Abstractions;

public sealed record VoucherQuery(string? Search, VoucherSource? Source, ApprovalStatus? ApprovalStatus, Guid? BatchId, int Page, int PageSize);
