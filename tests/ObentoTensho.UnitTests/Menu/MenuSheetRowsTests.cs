using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.UnitTests.Menu;

// BR-MNU-14: đọc dòng Excel thành dữ liệu món; lỗi kiểu dữ liệu báo đúng cột
public sealed class MenuSheetRowsTests
{
    [Fact]
    public void Dong_day_du_doc_dung_moi_cot()
    {
        var errors = new List<MenuImportError>();
        var parsed = MenuSheetRows.Parse(Row(new(StringComparer.Ordinal)
        {
            [MenuSheet.Slug] = "com-ga",
            [MenuSheet.Category] = "com-hop",
            [MenuSheet.Name] = "Cơm gà",
            [MenuSheet.Price] = "65.000",
            [MenuSheet.OriginalPrice] = "80000",
            [MenuSheet.Status] = "Hết hàng",
            [MenuSheet.Takeaway] = "Không",
            [MenuSheet.Spicy] = "x",
            [MenuSheet.Allergens] = "egg; MILK",
            [MenuSheet.DisplayOrder] = "3",
        }), errors);

        Assert.Empty(errors);
        Assert.Equal(65_000m, parsed.Price);
        Assert.Equal(80_000m, parsed.OriginalPrice);
        Assert.Equal(SaleStatus.SoldOut, parsed.Status);
        Assert.False(parsed.AllowTakeaway);
        Assert.True(parsed.Labels.IsSpicy);
        Assert.False(parsed.Labels.IsNew);
        Assert.Equal([Allergen.Egg, Allergen.Milk], parsed.Allergens);
        Assert.Equal(3, parsed.DisplayOrder);
    }

    [Fact]
    public void O_trong_dung_mac_dinh()
    {
        var errors = new List<MenuImportError>();
        var parsed = MenuSheetRows.Parse(Row(new(StringComparer.Ordinal) { [MenuSheet.Name] = "Cơm gà", [MenuSheet.Price] = "65000" }), errors);

        Assert.Empty(errors);
        Assert.Equal(SaleStatus.Available, parsed.Status);
        Assert.True(parsed.AllowTakeaway);
        Assert.Empty(parsed.Allergens);
        Assert.Null(parsed.DisplayOrder);
        Assert.Null(parsed.Slug);
    }

    [Theory]
    [InlineData(MenuSheet.Price, "-5000")]
    [InlineData(MenuSheet.Price, "65k")]
    [InlineData(MenuSheet.OriginalPrice, "1,2,3")]
    [InlineData(MenuSheet.Status, "Bán")]
    [InlineData(MenuSheet.Featured, "có lẽ")]
    [InlineData(MenuSheet.Allergens, "egg,gluten")]
    [InlineData(MenuSheet.DisplayOrder, "-1")]
    public void Sai_kieu_bao_dung_cot(string column, string value)
    {
        var errors = new List<MenuImportError>();
        MenuSheetRows.Parse(Row(new(StringComparer.Ordinal) { [MenuSheet.Name] = "Cơm gà", [MenuSheet.Price] = "65000", [column] = value }), errors);

        var error = Assert.Single(errors);
        Assert.Equal(column, error.Column);
        Assert.Equal(FieldErrorCodes.InvalidFormat, error.Code);
    }

    [Fact]
    public void O_co_cong_thuc_bi_tu_choi()
    {
        var errors = new List<MenuImportError>();
        var row = new MenuSheetRow(5, Cells(new(StringComparer.Ordinal) { [MenuSheet.Name] = "Cơm gà" }), new HashSet<string>([MenuSheet.Price], StringComparer.Ordinal));

        MenuSheetRows.Parse(row, errors);

        Assert.Contains(errors, e => string.Equals(e.Column, MenuSheet.Price, StringComparison.Ordinal) && string.Equals(e.Code, FieldErrorCodes.InvalidFormat, StringComparison.Ordinal));
    }

    [Fact]
    public void Xuat_roi_doc_lai_khong_doi()
    {
        var item = new MenuItem
        {
            CategoryId = 1, NameVi = "Cơm gà", Slug = "com-ga", NameSearch = "com ga", Price = 65_000, OriginalPrice = 80_000,
            IsBestseller = true, AllowTakeaway = false, Status = SaleStatus.Hidden, Allergens = "egg,sesame", DisplayOrder = 7,
            CreatedAtUtc = DateTime.UtcNow,
        };
        var values = MenuSheetRows.ToRow(item, "com-hop");
        var cells = MenuSheet.Columns.Select((c, i) => (c, v: values[i] is { } v ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null))
            .ToDictionary(x => x.c, x => x.v, StringComparer.Ordinal);

        var errors = new List<MenuImportError>();
        var parsed = MenuSheetRows.Parse(new MenuSheetRow(2, cells, new HashSet<string>(StringComparer.Ordinal)), errors);

        Assert.Empty(errors);
        Assert.Equal(("com-ga", "com-hop", "Cơm gà", 65_000m, 80_000m, SaleStatus.Hidden, false, 7), (parsed.Slug, parsed.CategorySlug, parsed.Name, parsed.Price, parsed.OriginalPrice, parsed.Status, parsed.AllowTakeaway, parsed.DisplayOrder ?? 0));
        Assert.True(parsed.Labels.IsBestseller);
        Assert.Equal([Allergen.Egg, Allergen.Sesame], parsed.Allergens);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "'=HYPERLINK(\"http://x\")")]
    [InlineData("+84 90", "'+84 90")]
    [InlineData("-giảm giá", "'-giảm giá")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("Cơm gà", "Cơm gà")]
    [InlineData("'trích dẫn", "'trích dẫn")]
    public void Chong_chen_cong_thuc_va_doc_lai_nguyen_van(string value, string guarded)
    {
        Assert.Equal(guarded, SpreadsheetText.Guard(value));
        Assert.Equal(value, SpreadsheetText.Unguard(SpreadsheetText.Guard(value)));
    }

    private static MenuSheetRow Row(Dictionary<string, string?> values) =>
        new(2, Cells(values), new HashSet<string>(StringComparer.Ordinal));

    private static Dictionary<string, string?> Cells(Dictionary<string, string?> values) =>
        MenuSheet.Columns.ToDictionary(c => c, values.GetValueOrDefault, StringComparer.Ordinal);
}
