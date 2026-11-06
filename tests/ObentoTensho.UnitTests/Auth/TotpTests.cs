using System.Text;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.UnitTests.Auth;

public sealed class TotpTests
{
    // RFC 6238 phụ lục B (SHA1, khóa "12345678901234567890"); RFC dùng 8 chữ số, 6 chữ số cuối là mã 6 số
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Ma_khop_vector_chuan_RFC_6238(long unixSeconds, string expected)
    {
        var step = Totp.GetStep(DateTime.UnixEpoch.AddSeconds(unixSeconds));

        Assert.Equal(expected, Totp.ComputeCode(RfcSecret, step));
    }

    [Fact]
    public void Chap_nhan_lech_mot_buoc_va_tu_choi_lech_hai_buoc()
    {
        var now = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
        var step = Totp.GetStep(now);

        Assert.Equal(step - 1, Totp.FindMatchingStep(RfcSecret, Totp.ComputeCode(RfcSecret, step - 1), now));
        Assert.Equal(step + 1, Totp.FindMatchingStep(RfcSecret, Totp.ComputeCode(RfcSecret, step + 1), now));
        Assert.Null(Totp.FindMatchingStep(RfcSecret, Totp.ComputeCode(RfcSecret, step - 2), now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void Ma_sai_dinh_dang_bi_tu_choi(string? code)
    {
        Assert.Null(Totp.FindMatchingStep(RfcSecret, code, DateTime.UtcNow));
    }

    [Fact]
    public void Base32_dung_RFC_4648()
    {
        Assert.Equal("MZXW6YTBOI", Base32.Encode("foobar"u8));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Base32.Encode(RfcSecret));
    }

    [Fact]
    public void Uri_cho_ung_dung_xac_thuc_co_du_tham_so()
    {
        var uri = Totp.BuildUri("Obento Tensho", "quanly@example.com", RfcSecret);

        Assert.StartsWith("otpauth://totp/Obento%20Tensho:quanly%40example.com?", uri, StringComparison.Ordinal);
        Assert.Contains("=" + Base32.Encode(RfcSecret) + "&", uri, StringComparison.Ordinal);
        Assert.Contains("issuer=Obento%20Tensho", uri, StringComparison.Ordinal);
        Assert.Contains("digits=6&period=30", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Ma_khoi_phuc_du_10_ma_khac_nhau_dang_XXXXX_XXXXX()
    {
        var codes = RecoveryCodes.Generate();

        Assert.Equal(10, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, c => Assert.Matches("^[A-Z2-7]{5}-[A-Z2-7]{5}$", c));
        Assert.Equal("ABCDE23456", RecoveryCodes.Normalize(" abcde-23456 "));
    }
}
