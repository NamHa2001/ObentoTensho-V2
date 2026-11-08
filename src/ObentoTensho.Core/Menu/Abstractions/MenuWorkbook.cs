namespace ObentoTensho.Core.Menu.Abstractions;

// Nội dung tệp xuất/mẫu: dòng món theo thứ tự MenuSheet.Columns, bảng tra danh mục (slug, tên), hướng dẫn
public sealed record MenuWorkbook(
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    IReadOnlyList<IReadOnlyList<object?>> CategoryRows,
    IReadOnlyList<string> Guide);
