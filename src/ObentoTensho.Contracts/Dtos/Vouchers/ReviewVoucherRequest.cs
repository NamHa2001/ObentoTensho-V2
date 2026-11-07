namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Duyệt/từ chối voucher (hoặc lô) đang chờ; người duyệt phải khác người tạo.</summary>
public sealed record ReviewVoucherRequest(string? Note);
