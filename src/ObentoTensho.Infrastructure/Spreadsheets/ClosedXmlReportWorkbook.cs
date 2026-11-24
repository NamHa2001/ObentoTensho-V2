using ClosedXML.Excel;
using ObentoTensho.Core.Reports.Abstractions;

namespace ObentoTensho.Infrastructure.Spreadsheets;

// docs/srs/23 FR-PLT-04: khối tiêu đề, dòng tên cột cố định, ô số là số (định dạng tiền VN), dòng tổng in đậm
internal sealed class ClosedXmlReportWorkbook : IReportWorkbook
{
    private const string MoneyFormat = "#,##0";
    private const string PercentFormat = "0.##\"%\"";
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm";

    public byte[] Write(IReadOnlyList<string> heading, IReadOnlyList<ReportTable> tables)
    {
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(tables);
        using var book = new XLWorkbook();
        foreach (var table in tables)
        {
            var sheet = book.AddWorksheet(table.Name);
            for (var i = 0; i < heading.Count; i++)
            {
                ClosedXmlCells.SetText(sheet.Cell(i + 1, 1), heading[i]);
            }

            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(2, 1).Style.Font.Bold = true;
            sheet.Cell(2, 1).Style.Font.FontSize = 14;

            var headerRow = heading.Count + 2;
            for (var c = 0; c < table.Columns.Count; c++)
            {
                ClosedXmlCells.SetText(sheet.Cell(headerRow, c + 1), table.Columns[c].Header);
            }

            var header = sheet.Range(headerRow, 1, headerRow, table.Columns.Count);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEEEEE");
            sheet.SheetView.FreezeRows(headerRow);

            var row = headerRow + 1;
            foreach (var values in table.Rows)
            {
                WriteRow(sheet, row++, table.Columns, values);
            }

            if (table.Totals is { } totals)
            {
                WriteRow(sheet, row, table.Columns, totals);
                sheet.Row(row).Style.Font.Bold = true;
                sheet.Range(row, 1, row, table.Columns.Count).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            sheet.Columns(1, table.Columns.Count).AdjustToContents(headerRow, row);
        }

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    private static void WriteRow(IXLWorksheet sheet, int row, IReadOnlyList<ReportColumn> columns, IReadOnlyList<object?> values)
    {
        for (var c = 0; c < columns.Count && c < values.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            switch (values[c])
            {
                case null:
                    break;
                case string text:
                    ClosedXmlCells.SetText(cell, text);
                    break;
                case DateTime at:
                    cell.Value = at;
                    cell.Style.NumberFormat.Format = DateTimeFormat;
                    break;
                case int count:
                    cell.Value = count;
                    break;
                case decimal number:
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = columns[c].Kind == ReportCellKind.Percent ? PercentFormat : MoneyFormat;
                    break;
                default:
                    ClosedXmlCells.SetText(cell, Convert.ToString(values[c], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
                    break;
            }
        }
    }
}
