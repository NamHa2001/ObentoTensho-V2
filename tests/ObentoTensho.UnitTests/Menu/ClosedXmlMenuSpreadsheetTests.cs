using System.IO.Compression;
using ClosedXML.Excel;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Infrastructure.Spreadsheets;

namespace ObentoTensho.UnitTests.Menu;

// BR-MNU-14, SEC-11: tệp Excel thực đơn — đọc lại đúng, chặn tệp hỏng/quá lớn, chống chèn công thức khi xuất
public sealed class ClosedXmlMenuSpreadsheetTests
{
    private readonly ClosedXmlMenuSpreadsheet _sheet = new();

    [Fact]
    public void Ghi_roi_doc_lai_dung_gia_tri_va_bo_dong_trong()
    {
        var bytes = _sheet.Write(new MenuWorkbook(
            [Values("com-ga", "Cơm gà", 65_000m), new object?[MenuSheet.Columns.Count], Values(null, "=HYPERLINK(\"http://evil\")", 1_000m)],
            [("com-hop", "Cơm hộp")],
            MenuSheet.Guide));

        var rows = Read(bytes);

        Assert.Equal(2, rows.Count);
        Assert.Equal((2, "com-ga", "Cơm gà", "65000"), (rows[0].RowNumber, rows[0][MenuSheet.Slug], rows[0][MenuSheet.Name], rows[0][MenuSheet.Price]));
        Assert.Equal(4, rows[1].RowNumber);
        Assert.Null(rows[1][MenuSheet.Slug]);
        Assert.Equal("=HYPERLINK(\"http://evil\")", rows[1][MenuSheet.Name]);
    }

    [Fact]
    public void Tep_xuat_khong_chua_cong_thuc_va_co_danh_muc_huong_dan()
    {
        var bytes = _sheet.Write(new MenuWorkbook([Values(null, "=1+1", 1_000m)], [("com-hop", "Cơm hộp")], MenuSheet.Guide));

        using var book = new XLWorkbook(new MemoryStream(bytes));
        var cell = book.Worksheet(MenuSheet.ItemsSheet).Cell(2, 3);
        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.True(cell.Style.IncludeQuotePrefix);
        Assert.Equal("=1+1", cell.GetString());
        Assert.Equal("com-hop", book.Worksheet(MenuSheet.CategoriesSheet).Cell(2, 1).GetString());
        Assert.Equal(MenuSheet.Guide.Count, book.Worksheet(MenuSheet.GuideSheet).LastRowUsed()!.RowNumber());
    }

    [Fact]
    public void O_cong_thuc_trong_tep_nhap_duoc_danh_dau()
    {
        var bytes = Build(sheet =>
        {
            sheet.Cell(2, 3).Value = "Cơm gà";
            sheet.Cell(2, 5).FormulaA1 = "60000+5000";
        });

        var row = Assert.Single(Read(bytes));

        Assert.Contains(MenuSheet.Price, row.FormulaColumns);
        Assert.Null(row[MenuSheet.Price]);
    }

    [Fact]
    public void Sai_tieu_de_hoac_khong_phai_excel_la_tep_khong_hop_le()
    {
        var wrongHeader = Build(sheet => sheet.Cell(1, 5).Value = "Price");
        var extraColumn = Build(sheet => sheet.Cell(1, MenuSheet.Columns.Count + 1).Value = "Ghi chú");

        Assert.Equal(ErrorCodes.FileInvalid, _sheet.Read(new MemoryStream(wrongHeader)).ErrorCode);
        Assert.Equal(ErrorCodes.FileInvalid, _sheet.Read(new MemoryStream(extraColumn)).ErrorCode);
        Assert.Equal(ErrorCodes.FileInvalid, _sheet.Read(new MemoryStream("Slug,Tên món\ncom-ga,Cơm gà"u8.ToArray())).ErrorCode);
        Assert.Equal(ErrorCodes.FileInvalid, _sheet.Read(new MemoryStream(ZipWith("hello.txt", new byte[10]))).ErrorCode);
    }

    [Fact]
    public void Qua_1000_dong_hoac_qua_2_MB_bi_tu_choi()
    {
        var exactly = Build(sheet => FillRows(sheet, MenuSheet.MaxRows));
        var tooMany = Build(sheet => FillRows(sheet, MenuSheet.MaxRows + 1));

        Assert.Equal(MenuSheet.MaxRows, Read(exactly).Count);
        Assert.Equal(ErrorCodes.FileTooLarge, _sheet.Read(new MemoryStream(tooMany)).ErrorCode);
        Assert.Equal(ErrorCodes.FileTooLarge, _sheet.Read(new MemoryStream(new byte[MenuSheet.MaxBytes + 1])).ErrorCode);
    }

    [Fact]
    public void Tep_nen_giai_ra_qua_lon_bi_tu_choi_truoc_khi_nap()
    {
        // 40 MB số 0 nén còn khoảng 40 KB
        var bomb = ZipWith("xl/worksheets/sheet1.xml", new byte[40 * 1024 * 1024]);

        Assert.True(bomb.Length < MenuSheet.MaxBytes);
        Assert.Equal(ErrorCodes.FileTooLarge, _sheet.Read(new MemoryStream(bomb)).ErrorCode);
    }

    private IReadOnlyList<MenuSheetRow> Read(byte[] bytes)
    {
        var result = _sheet.Read(new MemoryStream(bytes));
        Assert.True(result.IsSuccess, result.ErrorCode);
        return result.Value;
    }

    private static object?[] Values(string? slug, string name, decimal price)
    {
        var values = new object?[MenuSheet.Columns.Count];
        values[0] = slug;
        values[1] = "com-hop";
        values[2] = name;
        values[4] = price;
        return values;
    }

    private static void FillRows(IXLWorksheet sheet, int count)
    {
        for (var r = 0; r < count; r++)
        {
            sheet.Cell(r + 2, 3).Value = $"Món {r}";
            sheet.Cell(r + 2, 5).Value = 10_000;
        }
    }

    // Tệp đúng mẫu (tiêu đề chuẩn), sửa thêm tùy test
    private static byte[] Build(Action<IXLWorksheet> edit)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(MenuSheet.ItemsSheet);
        for (var c = 0; c < MenuSheet.Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = MenuSheet.Columns[c];
        }

        edit(sheet);
        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    private static byte[] ZipWith(string name, byte[] content)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = zip.CreateEntry(name, CompressionLevel.SmallestSize).Open();
            entry.Write(content);
        }

        return output.ToArray();
    }
}
