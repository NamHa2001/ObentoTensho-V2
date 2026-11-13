namespace ObentoTensho.Contracts.Enums;

/// <summary>Quyền theo module của trang quản trị (ma trận ở docs/srs/01 §3). Frontend dùng để ẩn/hiện menu; server luôn kiểm lại.</summary>
public enum StaffCapability
{
    Dashboard,
    Reservations,
    TablesView,
    TablesManage,
    Shifts,
    OrdersView,
    OrdersKitchen,
    OrdersCashier,
    OrdersCancel,
    Menu,
    Customers,
    Feedback,
    Games,
    Content,
    Reports,
    PaymentSettings,
    CustomerExport,
    RegularHours,
    SpecialHours,
    StaffAndAudit,

    // Gọi món tại bàn: mở bàn, gọi món, chuyển/gộp bàn
    DineIn,

    // Hủy món đã gửi bếp (chủ quán 2026-11-13: cần quản lý)
    OrdersVoid,

    // Kế toán xem và xử lý yêu cầu xuất hóa đơn (có số CCCD, MST)
    Invoices
}
