namespace ObentoTensho.Core.Reports.Abstractions;

// Bảng có kiểu từng cột để file Excel ghi số là số; Totals là dòng tổng cộng (in đậm), null nếu không có
public sealed record ReportTable(string Name, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, IReadOnlyList<object?>? Totals);
