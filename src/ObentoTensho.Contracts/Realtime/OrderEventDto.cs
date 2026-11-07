using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Realtime;

/// <summary>Gửi cả khi đổi thanh toán/xác nhận liên hệ để bảng bếp và quầy thu ngân tải lại đơn.</summary>
public sealed record OrderEventDto(Guid Id, string Code, OrderStatus Status, PaymentStatus PaymentStatus);
