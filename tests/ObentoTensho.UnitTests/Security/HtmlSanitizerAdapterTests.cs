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
    public void Bai_viet_giu_anh_nhung_mo_ta_mon_thi_khong()
    {
        const string html = "<h2>Tin</h2><img src=\"/media/a.webp\" alt=\"a\">";

        Assert.Equal(html, _sanitizer.SanitizeRich(html));
        Assert.Empty(_sanitizer.Sanitize(html));
    }

    [Theory]
    [InlineData("<img src=\"javascript:alert(1)\">")]
    [InlineData("<img src=\"data:image/svg+xml,x\">")]
    [InlineData("<img src=\"/a.webp\" onerror=\"alert(1)\">")]
    [InlineData("<svg onload=\"alert(1)\"></svg>")]
    [InlineData("<iframe src=\"https://evil.test\"></iframe>")]
    public void Bai_viet_van_bo_ma_chay_duoc(string html)
    {
        var clean = _sanitizer.SanitizeRich(html);

        Assert.DoesNotContain("alert", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("data:", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", clean, StringComparison.Ordinal);
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
