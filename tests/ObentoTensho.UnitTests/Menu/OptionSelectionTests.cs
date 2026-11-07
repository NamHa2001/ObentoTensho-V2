using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.UnitTests.Menu;

// BR-MNU-20/21: server tự kiểm lựa chọn tùy chọn và tự tính tiền cộng thêm
public sealed class OptionSelectionTests
{
    // Nhóm 1 "Size": bắt buộc chọn một (10 nhỏ, 11 lớn +15.000)
    // Nhóm 2 "Topping": tùy ý, tối đa 2 (20 trứng +5.000, 21 phô mai +10.000 hết hàng, 22 rong biển +3.000)
    private static IReadOnlyList<OptionSelection.GroupOptions> Groups(bool toppingActive = true) =>
    [
        new(Group(1, OptionSelectionType.One, required: true, min: 1, max: 1), [Option(10, 1, 0), Option(11, 1, 15_000)]),
        new(
            Group(2, OptionSelectionType.Many, required: false, min: 0, max: 2, active: toppingActive),
            [Option(20, 2, 5_000), Option(21, 2, 10_000, SaleStatus.SoldOut), Option(22, 2, 3_000)]),
    ];

    [Fact]
    public void Hop_le_thi_cong_dung_tien_tu_CSDL()
    {
        var outcome = OptionSelection.Check(Groups(), [11, 20, 22]);

        Assert.True(outcome.IsValid);
        Assert.Equal(23_000, outcome.PriceDelta);
        Assert.Equal([11, 20, 22], outcome.Selected.Select(o => o.Id));
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 10, 11 })]
    [InlineData(new[] { 10, 20, 22, 21 })]
    [InlineData(new[] { 10, 99 })]
    [InlineData(new[] { 10, 20, 20 })]
    public void Sai_so_luong_trung_hoac_la_thi_tu_choi(int[] selected)
    {
        var outcome = OptionSelection.Check(Groups(), selected);

        Assert.Equal(ErrorCodes.OptionInvalid, outcome.ErrorCode);
        Assert.Equal(0, outcome.PriceDelta);
    }

    [Fact]
    public void Tuy_chon_het_hang_bao_loi_rieng()
    {
        var outcome = OptionSelection.Check(Groups(), [10, 21]);

        Assert.Equal(ErrorCodes.OptionUnavailable, outcome.ErrorCode);
        Assert.Contains("option_21_unavailable", outcome.Problems, StringComparer.Ordinal);
    }

    [Fact]
    public void Nhom_da_tat_khong_tinh_va_khong_chon_duoc()
    {
        Assert.True(OptionSelection.Check(Groups(toppingActive: false), [10]).IsValid);
        Assert.Equal(ErrorCodes.OptionInvalid, OptionSelection.Check(Groups(toppingActive: false), [10, 20]).ErrorCode);
    }

    private static OptionGroup Group(int id, OptionSelectionType type, bool required, byte min, byte max, bool active = true) =>
        WithId(new OptionGroup { Name = $"G{id}", SelectionType = type, IsRequired = required, MinSelect = min, MaxSelect = max, IsActive = active }, id);

    private static MenuOption Option(int id, int groupId, decimal delta, SaleStatus status = SaleStatus.Available) =>
        WithId(new MenuOption { GroupId = groupId, Name = $"O{id}", PriceDelta = delta, Status = status }, id);

    // Id do CSDL sinh nên không có setter công khai
    private static T WithId<T>(T entity, int id)
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
        return entity;
    }
}
