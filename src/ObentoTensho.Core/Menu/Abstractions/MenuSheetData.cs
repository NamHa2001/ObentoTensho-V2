namespace ObentoTensho.Core.Menu.Abstractions;

// Dữ liệu đọc từ tệp nhập: dòng món và dòng danh mục (sheet "Danh mục" có thể không có)
public sealed record MenuSheetData(IReadOnlyList<MenuSheetRow> Items, IReadOnlyList<MenuSheetRow> Categories);
