using ClosedXML.Excel;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Infrastructure.Spreadsheets;

internal sealed class ClosedXmlTableSpreadsheet : ITableSpreadsheet
{
    public byte[] Write(string sheetName, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(sheetName);
        for (var c = 0; c < columns.Count; c++)
        {
            ClosedXmlCells.SetText(sheet.Cell(1, c + 1), columns[c]);
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, columns.Count).Width = 24;
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Count; c++)
            {
                ClosedXmlCells.SetText(sheet.Cell(r + 2, c + 1), rows[r][c]);
            }
        }

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }
}
