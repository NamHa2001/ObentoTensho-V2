using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Một dòng trong báo cáo nhập: <c>Row</c> là số dòng trong Excel (dòng tiêu đề là 1); không có lỗi = hợp lệ.</summary>
public sealed record MenuImportRow(int Row, string? Slug, string? Name, MenuImportAction Action, IReadOnlyList<MenuImportError> Errors);
