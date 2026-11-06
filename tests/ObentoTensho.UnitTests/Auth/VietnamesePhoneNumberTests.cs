using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.UnitTests.Auth;

// BR-AUTH-13
public sealed class VietnamesePhoneNumberTests
{
    [Theory]
    [InlineData("0912345678", "+84912345678")]
    [InlineData("0912 345 678", "+84912345678")]
    [InlineData("091.234.5678", "+84912345678")]
    [InlineData("+84 912 345 678", "+84912345678")]
    [InlineData("+84912345678", "+84912345678")]
    [InlineData("84912345678", "+84912345678")]
    [InlineData("0387654321", "+84387654321")]
    [InlineData("(024) 3825 1234", "+842438251234")]
    public void Chuan_hoa_ve_dang_quoc_te(string input, string expected)
    {
        Assert.True(VietnamesePhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("091234567")]
    [InlineData("09123456789")]
    [InlineData("0112345678")]
    [InlineData("+1 912 345 678")]
    [InlineData("09a2345678")]
    [InlineData("091+2345678")]
    [InlineData("912345678")]
    public void So_khong_hop_le_bi_tu_choi(string? input)
    {
        Assert.False(VietnamesePhoneNumber.TryNormalize(input, out _));
    }
}
