namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Xem trước tổng tiền: mã giảm giá hoặc SĐT thành viên (giảm theo hạng, tích điểm). Có mã thì không giảm theo hạng (BR-ORD-03).</summary>
public sealed record DineInQuoteRequest(string? VoucherCode, string? MemberPhone);
