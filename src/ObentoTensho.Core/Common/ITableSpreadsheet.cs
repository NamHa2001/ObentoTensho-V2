namespace ObentoTensho.Core.Common;

// Xuất bảng ra Excel, mọi ô ghi dạng văn bản để chống formula injection (SEC-11)
public interface ITableSpreadsheet
{
    byte[] Write(string sheetName, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows);
}
