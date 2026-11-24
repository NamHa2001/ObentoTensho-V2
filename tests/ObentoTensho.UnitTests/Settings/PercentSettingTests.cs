using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.UnitTests.Settings;

// docs/srs/23 BR-PLT-04: tỉ lệ khấu trừ, thuế lưu dạng phần trăm có số lẻ (1.5), 0–100
public sealed class PercentSettingTests
{
    private static readonly SettingDefinition Rate = new("revenue.pitPercent", SettingType.Percent, "1.5", "Thuế TNCN", 0, 100);

    [Theory]
    [InlineData("1.5", null)]
    [InlineData("25", null)]
    [InlineData("0", null)]
    [InlineData("100", null)]
    [InlineData("12.25", null)]
    [InlineData("1.555", FieldErrorCodes.InvalidFormat)]
    [InlineData("1,5", FieldErrorCodes.InvalidFormat)]
    [InlineData("-1", FieldErrorCodes.InvalidFormat)]
    [InlineData("abc", FieldErrorCodes.InvalidFormat)]
    [InlineData("100.5", FieldErrorCodes.OutOfRange)]
    public void Kiem_phan_tram(string value, string? expected) => Assert.Equal(expected, SettingRules.Check(Rate, value));

    [Fact]
    public void Mac_dinh_ba_ti_le_tong_29_5()
    {
        var total = new[] { SettingKeys.RevenueDeductionPercent, SettingKeys.RevenueVatPercent, SettingKeys.RevenuePitPercent }
            .Sum(k => decimal.Parse(SettingsCatalog.All.Single(d => string.Equals(d.Key, k, StringComparison.Ordinal)).Default, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(29.5m, total);
    }
}
