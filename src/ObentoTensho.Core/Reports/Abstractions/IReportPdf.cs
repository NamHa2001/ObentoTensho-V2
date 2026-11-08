namespace ObentoTensho.Core.Reports.Abstractions;

// D-04, BR-RPT-13: báo cáo dạng PDF để in/gửi; cùng bảng dữ liệu với bản Excel
public interface IReportPdf
{
    byte[] Write(string title, string subtitle, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows);
}
