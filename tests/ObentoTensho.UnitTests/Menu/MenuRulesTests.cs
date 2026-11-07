using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.UnitTests.Menu;

// docs/srs/15 §8: giá, trạng thái hết hàng có hạn, dị ứng, slug, băm bản gốc cho bản dịch
public sealed class MenuRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Het_hang_co_han_tu_ban_lai_khi_qua_moc()
    {
        Assert.Equal(SaleStatus.Available, MenuRules.Effective(SaleStatus.SoldOut, Now.AddMinutes(-1), Now));
        Assert.Equal(SaleStatus.SoldOut, MenuRules.Effective(SaleStatus.SoldOut, Now.AddMinutes(1), Now));
        Assert.Equal(SaleStatus.SoldOut, MenuRules.Effective(SaleStatus.SoldOut, null, Now));
        Assert.Equal(SaleStatus.Hidden, MenuRules.Effective(SaleStatus.Hidden, Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void Di_ung_luu_dang_ma_sap_xep_va_doc_lai_duoc()
    {
        var stored = MenuRules.JoinAllergens([Allergen.TreeNut, Allergen.Egg, Allergen.Egg]);

        Assert.Equal("egg,tree_nut", stored);
        Assert.Equal([Allergen.Egg, Allergen.TreeNut], MenuRules.ParseAllergens(stored));
        Assert.Null(MenuRules.JoinAllergens([]));
    }

    [Fact]
    public void Tham_so_loc_di_ung_bo_qua_ma_la_va_khong_phan_biet_hoa_thuong()
    {
        Assert.Equal([Allergen.Milk, Allergen.Peanut], MenuRules.ParseAllergens(" MILK ,abc,peanut,"));
        Assert.Empty(MenuRules.ParseAllergens("<script>"));
        Assert.Empty(MenuRules.ParseAllergens(null));
    }

    [Fact]
    public void Bam_ban_goc_doi_khi_noi_dung_doi()
    {
        var hash = MenuRules.SourceHash("Cơm gà", "Ngon", null);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, MenuRules.SourceHash("Cơm gà", "Ngon", string.Empty));
        Assert.NotEqual(hash, MenuRules.SourceHash("Cơm gà ", "Ngon", null), StringComparer.Ordinal);

        // Ranh giới trường không bị nhập nhằng khi ghép
        Assert.NotEqual(MenuRules.SourceHash("ab", "c"), MenuRules.SourceHash("a", "bc"), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Cơm gà Hội An", "com-ga-hoi-an")]
    [InlineData("Đậu phụ sốt cà chua!!", "dau-phu-sot-ca-chua")]
    [InlineData("  --Bento   số 1-- ", "bento-so-1")]
    public void Slug_bo_dau_va_ky_tu_la(string name, string slug) =>
        Assert.Equal(slug, TextNormalizer.Slug(name));

    [Fact]
    public void Chuoi_tim_kiem_bo_dau_gop_ban_dich()
    {
        Assert.Equal("com ga hoi an chicken rice", TextNormalizer.Search("Cơm  gà Hội An", null, "Chicken Rice"));
    }

    [Fact]
    public void Gia_mon_phai_duong_nguyen_va_gia_goc_lon_hon()
    {
        var validator = new MenuItemRequestValidator();

        Assert.True(validator.Validate(Item(65_000, 80_000)).IsValid);
        Assert.False(validator.Validate(Item(0, null)).IsValid);
        Assert.False(validator.Validate(Item(65_000.5m, null)).IsValid);
        Assert.False(validator.Validate(Item(65_000, 65_000)).IsValid);
        Assert.False(validator.Validate(Item(MenuRules.MaxPrice + 1, null)).IsValid);
        Assert.False(validator.Validate(Item(65_000, null) with { OptionGroupIds = [1, 1] }).IsValid);
        Assert.False(validator.Validate(Item(65_000, null) with { OptionGroupIds = [.. Enumerable.Range(1, 11)] }).IsValid);
        Assert.False(validator.Validate(Item(65_000, null) with { Slug = "Có Dấu" }).IsValid);
    }

    [Theory]
    [InlineData(OptionSelectionType.One, false, 0, 1, true)]
    [InlineData(OptionSelectionType.One, false, 0, 2, false)]
    [InlineData(OptionSelectionType.Many, true, 0, 3, false)]
    [InlineData(OptionSelectionType.Many, true, 1, 3, true)]
    [InlineData(OptionSelectionType.Many, false, 4, 3, false)]
    [InlineData(OptionSelectionType.Many, false, 0, 21, false)]
    public void Nhom_tuy_chon_kiem_min_max(OptionSelectionType type, bool required, int min, int max, bool valid) =>
        Assert.Equal(valid, new OptionGroupRequestValidator().Validate(new OptionGroupRequest("Size", type, required, min, max, true)).IsValid);

    private static MenuItemRequest Item(decimal price, decimal? original) => new(
        1, "Cơm gà", null, null, null, price, original, new MenuLabels(false, false, false, false, false), true, SaleStatus.Available, [], []);
}
