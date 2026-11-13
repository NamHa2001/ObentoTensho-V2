using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Thanh toán và đóng bàn.</summary>
public sealed record DineInCheckoutRequest(PaymentMethod PaymentMethod, string? VoucherCode, string? MemberPhone);
