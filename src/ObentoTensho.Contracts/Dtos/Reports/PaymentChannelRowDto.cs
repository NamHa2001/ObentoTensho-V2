using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Kênh con: nhóm Tại quán theo cách thu (tiền mặt, chuyển khoản), nhóm app theo app (Grab, Shopee, Be, Xanh).</summary>
public sealed record PaymentChannelRowDto(PaymentMethod? PaymentMethod, Platform? Platform, PaymentAmountsDto Amounts);
