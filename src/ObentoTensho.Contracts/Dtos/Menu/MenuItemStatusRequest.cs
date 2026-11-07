using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Đổi trạng thái nhanh. Hết hàng mặc định tới hết hôm nay (giờ VN) rồi tự bán lại (BR-MNU-16).</summary>
public sealed record MenuItemStatusRequest(SaleStatus Status, DateTime? SoldOutUntilUtc);
