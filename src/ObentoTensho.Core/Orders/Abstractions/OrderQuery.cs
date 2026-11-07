using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Orders.Abstractions;

// Danh sách đơn cho thu ngân/quản lý: lọc trạng thái, ngày lấy (giờ VN), tìm theo mã/tên/SĐT, đơn chờ hoàn tiền
public sealed record OrderQuery(OrderStatus? Status, DateOnly? From, DateOnly? To, string? Search, bool RefundPending, int Page, int PageSize);
