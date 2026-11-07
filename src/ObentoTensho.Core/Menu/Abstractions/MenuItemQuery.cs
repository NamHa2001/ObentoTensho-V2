using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Menu.Abstractions;

// Danh sách món cho trang quản trị (gồm cả món ẩn, không gồm món đã xóa); pageSize ≤ 100 (BR-MNU-03)
public sealed record MenuItemQuery(int? CategoryId, SaleStatus? Status, string? Search, int Page, int PageSize);
