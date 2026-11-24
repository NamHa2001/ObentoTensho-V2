namespace ObentoTensho.Core.Reports.Abstractions;

// D-04, BR-RPT-13: báo cáo dạng PDF để in/gửi; cùng bảng dữ liệu với bản Excel
public interface IReportPdf
{
    byte[] Write(string title, string subtitle, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows);

    // docs/srs/23 FR-PLT-04: A4 ngang, nhiều bảng nối nhau, cột số canh phải, dòng tổng in đậm
    byte[] WriteTables(string title, string subtitle, IReadOnlyList<ReportTable> tables);
}
