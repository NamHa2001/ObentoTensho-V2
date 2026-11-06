using System.Security.Cryptography;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.UnitTests.Security;

// SEC-16: thiếu hoặc yếu khóa ký thì không cho khởi động
public sealed class JwtOptionsTests
{
    private static JwtOptions WithKey(string kid, string key, string? activeKid = null)
    {
        var options = new JwtOptions { ActiveKeyId = activeKid ?? kid };
        options.SigningKeys[kid] = key;
        return options;
    }

    private static string RandomKey(int bytes = 32) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    [Fact]
    public void Khoa_ngau_nhien_256_bit_hop_le()
    {
        Assert.Null(WithKey("k1", RandomKey()).Validate());
    }

    [Fact]
    public void Thieu_khoa_bi_tu_choi()
    {
        Assert.NotNull(new JwtOptions().Validate());
    }

    [Fact]
    public void Khoa_ngan_hon_256_bit_bi_tu_choi()
    {
        Assert.NotNull(WithKey("k1", RandomKey(16)).Validate());
    }

    [Fact]
    public void Khoa_mau_toan_so_0_bi_tu_choi()
    {
        Assert.NotNull(WithKey("k1", Convert.ToBase64String(new byte[64])).Validate());
    }

    [Fact]
    public void Khoa_khong_phai_base64_bi_tu_choi()
    {
        Assert.NotNull(WithKey("k1", "CHANGE_ME_CHANGE_ME_CHANGE_ME_CHANGE_ME_!!").Validate());
    }

    [Fact]
    public void Kid_dang_dung_khong_ton_tai_bi_tu_choi()
    {
        Assert.NotNull(WithKey("k1", RandomKey(), activeKid: "k2").Validate());
    }

    [Fact]
    public void Danh_sach_mat_khau_pho_bien_duoc_nhung_va_nap_duoc()
    {
        Assert.True(EmbeddedCommonPasswordList.Count > 50);
        Assert.True(new EmbeddedCommonPasswordList().Contains("Matkhau123"));
    }
}
