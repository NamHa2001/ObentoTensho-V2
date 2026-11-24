namespace ObentoTensho.Core.Reports.Abstractions;

// docs/srs/23 FR-PLT-04: mỗi bảng một sheet; khối tiêu đề ở đầu sheet, cố định dòng tên cột, cột số là kiểu số
public interface IReportWorkbook
{
    byte[] Write(IReadOnlyList<string> heading, IReadOnlyList<ReportTable> tables);
}
