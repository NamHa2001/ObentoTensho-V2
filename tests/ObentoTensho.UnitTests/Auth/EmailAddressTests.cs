using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.UnitTests.Auth;

public sealed class EmailAddressTests
{
    [Fact]
    public void Normalize_cat_khoang_trang_va_ve_chu_thuong()
    {
        Assert.Equal("an.nguyen@example.com", EmailAddress.Normalize("  An.Nguyen@Example.COM "));
    }

    [Fact]
    public void Normalize_bo_dau_cham_thua_cuoi_ten_mien()
    {
        Assert.Equal("an@example.com", EmailAddress.Normalize("an@example.com."));
    }

    // BR-AUTH-13: một người không lấy được nhiều ưu đãi bằng +tag hay dấu chấm của Gmail
    [Theory]
    [InlineData("an.nguyen+uudai@gmail.com", "annguyen@gmail.com")]
    [InlineData("a.n.nguyen@googlemail.com", "annguyen@gmail.com")]
    [InlineData("an.nguyen+tag@example.com", "an.nguyen@example.com")]
    [InlineData("an.nguyen@example.com", "an.nguyen@example.com")]
    public void Canonicalize_bo_tag_va_dau_cham_gmail(string email, string expected)
    {
        Assert.Equal(expected, EmailAddress.Canonicalize(email));
    }
}
