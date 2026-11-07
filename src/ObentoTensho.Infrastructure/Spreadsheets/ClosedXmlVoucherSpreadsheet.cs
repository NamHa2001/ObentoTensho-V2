using System.Globalization;
using ClosedXML.Excel;
using ObentoTensho.Core.Vouchers.Abstractions;

namespace ObentoTensho.Infrastructure.Spreadsheets;

// BR-VCH-15: một sheet, dòng tiêu đề cố định; độ rộng đặt sẵn vì AdjustToContents phải nạp font hệ thống
internal sealed class ClosedXmlVoucherSpreadsheet : IVoucherSpreadsheet
{
    private const string SheetName = "Voucher";

    public byte[] Write(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(SheetName);
        for (var c = 0; c < columns.Count; c++)
        {
            ClosedXmlCells.SetText(sheet.Cell(1, c + 1), columns[c]);
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, columns.Count).Width = 14;
        sheet.Column(1).Width = 26;
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Count; c++)
            {
                Write(sheet.Cell(r + 2, c + 1), rows[r][c]);
            }
        }

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    private static void Write(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case decimal number:
                cell.Value = number;
                break;
            case int number:
                cell.Value = number;
                break;
            default:
                ClosedXmlCells.SetText(cell, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }
}
