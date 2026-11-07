namespace ObentoTensho.Core.Common;

// SEC-11: ô văn bản bắt đầu bằng = + - @ (hoặc tab, xuống dòng) có thể bị Excel/LibreOffice hiểu là công thức khi mở tệp xuất.
// Xuất: thêm dấu ' phía trước. Nhập lại tệp đã xuất: bỏ dấu ' đó để dữ liệu giữ nguyên.
public static class SpreadsheetText
{
    private static readonly char[] Triggers = ['=', '+', '-', '@', '\t', '\r'];

    public static string Guard(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length > 0 && Triggers.Contains(value[0]) ? "'" + value : value;
    }

    public static string Unguard(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length > 1 && value[0] == '\'' && Triggers.Contains(value[1]) ? value[1..] : value;
    }
}
