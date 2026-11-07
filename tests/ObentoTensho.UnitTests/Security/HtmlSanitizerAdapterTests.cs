using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.UnitTests.Security;

// SEC-08: mô tả món do nhân viên nhập được làm sạch trước khi lưu
public sealed class HtmlSanitizerAdapterTests
{
    private readonly HtmlSanitizerAdapter _sanitizer = new();

    [Fact]
    public void Giu_dinh_dang_co_ban()
    {
        Assert.Equal("<p><strong>Cơm</strong> <em>gà</em></p><ul><li>Trứng</li></ul>", _sanitizer.Sanitize("<p><strong>Cơm</strong> <em>gà</em></p><ul><li>Trứng</li></ul>"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script><p>x</p>")]
    [InlineData("<p onclick=\"alert(1)\">x</p>")]
    [InlineData("<img src=x onerror=alert(1)><p>x</p>")]
    [InlineData("<iframe src=\"https://evil.test\"></iframe><p>x</p>")]
    [InlineData("<style>p{}</style><p style=\"color:red\">x</p>")]
    public void Bo_ma_chay_duoc_va_thuoc_tinh_nguy_hiem(string html)
    {
        var clean = _sanitizer.Sanitize(html);

        Assert.Equal("<p>x</p>", clean);
    }

    [Fact]
    public void Lien_ket_chi_nhan_http_va_them_rel()
    {
        Assert.DoesNotContain("javascript", _sanitizer.Sanitize("<a href=\"javascript:alert(1)\">x</a>"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "<a href=\"https://obentotensho.vn/\" rel=\"noopener noreferrer nofollow\">x</a>",
            _sanitizer.Sanitize("<a href=\"https://obentotensho.vn/\" target=\"_blank\">x</a>"));
    }
}
