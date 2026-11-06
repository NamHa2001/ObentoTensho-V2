using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Đổi trạng thái; <c>ExpectedStatus</c> chặn hai người xử lý cùng lúc (DB-23). Từ chối/hủy/khôi phục no-show bắt buộc lý do.</summary>
public sealed record ChangeReservationStatusRequest(ReservationStatus ExpectedStatus, ReservationStatus Status, string? Reason);
