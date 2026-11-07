using ClosedXML.Excel;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Infrastructure.Spreadsheets;

internal static class ClosedXmlCells
{
    // SEC-11: mọi văn bản ghi dưới dạng chuỗi (định dạng "@"). Ô bắt đầu bằng ký tự công thức bật cờ quotePrefix —
    // tương đương gõ dấu ' trong Excel: ô hiện đúng chữ, Excel/LibreOffice không bao giờ tính như công thức.
    public static void SetText(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.NumberFormat.Format = "@";
        if (!string.Equals(SpreadsheetText.Guard(text), text, StringComparison.Ordinal))
        {
            cell.Style.IncludeQuotePrefix = true;
        }
    }
}
