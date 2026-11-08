using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Spreadsheets;

// BR-MNU-14 bằng ClosedXML. Tệp tải lên là dữ liệu không tin cậy: giới hạn dung lượng trước khi đọc,
// kiểm kích thước giải nén (chặn "zip bomb") trước khi ClosedXML nạp toàn bộ tệp vào bộ nhớ.
// Độ rộng cột đặt cố định: AdjustToContents phải nạp font hệ thống để đo chữ — chậm và phụ thuộc font của máy chủ.
internal sealed class ClosedXmlMenuSpreadsheet : IMenuSpreadsheet
{
    // Tệp 1.000 dòng thật chỉ vài trăm KB khi giải nén; 30 MB là trần rộng mà vẫn an toàn
    private const long MaxUncompressedBytes = 30 * 1024 * 1024;
    private const int MaxZipEntries = 200;

    public byte[] Write(MenuWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        using var book = new XLWorkbook();

        var items = book.AddWorksheet(MenuSheet.ItemsSheet);
        WriteRow(items, 1, MenuSheet.Columns);
        items.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < workbook.Rows.Count; i++)
        {
            WriteRow(items, i + 2, workbook.Rows[i]);
        }

        items.SheetView.FreezeRows(1);
        items.Columns(1, MenuSheet.Columns.Count).Width = 14;
        items.Column(1).Width = 30;
        items.Column(3).Width = 36;
        items.Column(4).Width = 40;

        var categories = book.AddWorksheet(MenuSheet.CategoriesSheet);
        WriteRow(categories, 1, MenuSheet.CategoryColumns);
        categories.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < workbook.CategoryRows.Count; i++)
        {
            WriteRow(categories, i + 2, workbook.CategoryRows[i]);
        }

        categories.SheetView.FreezeRows(1);
        categories.Column(1).Width = 30;
        categories.Column(2).Width = 36;
        categories.Columns(3, 4).Width = 12;

        var guide = book.AddWorksheet(MenuSheet.GuideSheet);
        for (var i = 0; i < workbook.Guide.Count; i++)
        {
            ClosedXmlCells.SetText(guide.Cell(i + 1, 1), workbook.Guide[i]);
        }

        guide.Column(1).Width = 120;

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    public Result<MenuSheetData> Read(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var buffer = new MemoryStream();
        if (!CopyWithLimit(content, buffer, MenuSheet.MaxBytes))
        {
            return Result.Failure<MenuSheetData>(ErrorCodes.FileTooLarge);
        }

        if (CheckArchive(buffer) is { } archiveError)
        {
            return Result.Failure<MenuSheetData>(archiveError);
        }

        try
        {
            buffer.Position = 0;
            using var book = new XLWorkbook(buffer);
            if (!book.TryGetWorksheet(MenuSheet.ItemsSheet, out var itemsSheet))
            {
                return Result.Failure<MenuSheetData>(ErrorCodes.FileInvalid);
            }

            var items = ReadRows(itemsSheet, MenuSheet.Columns);
            if (!items.IsSuccess)
            {
                return Result.Failure<MenuSheetData>(items.ErrorCode!);
            }

            // Sheet "Danh mục" không bắt buộc: tệp chỉ có món vẫn nhập được
            var categories = book.TryGetWorksheet(MenuSheet.CategoriesSheet, out var categoriesSheet)
                ? ReadRows(categoriesSheet, MenuSheet.CategoryColumns)
                : Result.Success<IReadOnlyList<MenuSheetRow>>([]);
            return categories.IsSuccess
                ? Result.Success(new MenuSheetData(items.Value, categories.Value))
                : Result.Failure<MenuSheetData>(categories.ErrorCode!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // Tệp hỏng/không phải Excel: ClosedXML và OpenXML ném nhiều loại ngoại lệ khác nhau
            return Result.Failure<MenuSheetData>(ErrorCodes.FileInvalid);
        }
    }

    private static Result<IReadOnlyList<MenuSheetRow>> ReadRows(IXLWorksheet sheet, IReadOnlyList<string> columns)
    {
        var header = Enumerable.Range(1, columns.Count).Select(c => sheet.Cell(1, c).GetString().Trim()).ToArray();
        if (!header.SequenceEqual(columns, StringComparer.Ordinal) || !sheet.Cell(1, columns.Count + 1).IsEmpty())
        {
            return Result.Failure<IReadOnlyList<MenuSheetRow>>(ErrorCodes.FileInvalid);
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<MenuSheetRow>();
        for (var r = 2; r <= lastRow; r++)
        {
            var cells = new Dictionary<string, string?>(StringComparer.Ordinal);
            var formulas = new HashSet<string>(StringComparer.Ordinal);
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                if (cell.HasFormula)
                {
                    formulas.Add(columns[c]);
                    cells[columns[c]] = null;
                    continue;
                }

                cells[columns[c]] = ReadCell(cell);
            }

            // Dòng trống giữa bảng bỏ qua; đếm giới hạn theo dòng có dữ liệu
            if (formulas.Count == 0 && cells.Values.All(v => v is null))
            {
                continue;
            }

            if (rows.Count == MenuSheet.MaxRows)
            {
                return Result.Failure<IReadOnlyList<MenuSheetRow>>(ErrorCodes.FileTooLarge);
            }

            rows.Add(new MenuSheetRow(r, cells, formulas));
        }

        return Result.Success<IReadOnlyList<MenuSheetRow>>(rows);
    }

    private static string? ReadCell(IXLCell cell)
    {
        var value = cell.Value;
        var text = value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Number => value.GetNumber().ToString("0.############", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "true" : "false",
            _ => SpreadsheetText.Unguard(cell.GetString().Trim()),
        };
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static void WriteRow(IXLWorksheet sheet, int row, IReadOnlyList<object?> values)
    {
        for (var c = 0; c < values.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            switch (values[c])
            {
                case null:
                    break;
                case string text:
                    ClosedXmlCells.SetText(cell, text);
                    break;
                case decimal number:
                    cell.Value = number;
                    break;
                case int number:
                    cell.Value = number;
                    break;
                default:
                    ClosedXmlCells.SetText(cell, Convert.ToString(values[c], CultureInfo.InvariantCulture) ?? string.Empty);
                    break;
            }
        }
    }

    private static bool CopyWithLimit(Stream source, MemoryStream target, long limit)
    {
        var chunk = new byte[81920];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (target.Length + read > limit)
            {
                return false;
            }

            target.Write(chunk, 0, read);
        }

        return true;
    }

    private static string? CheckArchive(MemoryStream buffer)
    {
        try
        {
            buffer.Position = 0;
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > MaxZipEntries)
            {
                return ErrorCodes.FileTooLarge;
            }

            // Kích thước khai trong tệp có thể bị làm giả: giải nén thật và đếm byte, vượt ngưỡng thì dừng ngay (SEC-11)
            return zip.Entries.Sum(e => e.Length) > MaxUncompressedBytes || ActualSize(zip) > MaxUncompressedBytes ? ErrorCodes.FileTooLarge : null;
        }
        catch (InvalidDataException)
        {
            return ErrorCodes.FileInvalid;
        }
    }

    private static long ActualSize(ZipArchive zip)
    {
        var chunk = new byte[81920];
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                total += read;
                if (total > MaxUncompressedBytes)
                {
                    return total;
                }
            }
        }

        return total;
    }
}
