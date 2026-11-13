using ObentoTensho.Contracts.Dtos.Invoices;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Thanh toán và đóng bàn. <c>Invoice</c>: khách lấy hóa đơn (bỏ trống nếu không).</summary>
public sealed record DineInCheckoutRequest(PaymentMethod PaymentMethod, string? VoucherCode, string? MemberPhone, InvoiceBuyerRequest? Invoice = null);
