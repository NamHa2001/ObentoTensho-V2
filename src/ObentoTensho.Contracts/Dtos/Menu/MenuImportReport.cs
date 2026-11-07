namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>
/// Kết quả nhập món từ Excel (BR-MNU-14). <c>Committed</c> = đã ghi vào CSDL; chỉ ghi khi gọi với <c>commit=true</c>
/// và không dòng nào lỗi (tất cả hoặc không). Xem trước (<c>commit=false</c>) trả cùng báo cáo nhưng không ghi.
/// </summary>
public sealed record MenuImportReport(bool Committed, int Total, int Created, int Updated, int Failed, IReadOnlyList<MenuImportRow> Rows);
