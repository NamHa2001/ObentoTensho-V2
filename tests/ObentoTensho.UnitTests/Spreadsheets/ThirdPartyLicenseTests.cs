namespace ObentoTensho.UnitTests.Spreadsheets;

// D-04: SixLabors.Fonts đi kèm ClosedXML. Bản 1.x dùng Apache-2.0; từ 2.x đổi sang Six Labors Split License
// (dùng thương mại phải mua khi doanh thu vượt ngưỡng). ClosedXML cho phép [1.0.0, 3.0.0) nên một gói khác có thể
// kéo bản 2.x vào mà không ai để ý — test này chặn lại để người nâng cấp phải xem điều khoản trước
public sealed class ThirdPartyLicenseTests
{
    [Fact]
    public void SixLabors_Fonts_van_la_ban_1_giay_phep_Apache()
    {
        var version = typeof(SixLabors.Fonts.FontCollection).Assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.Equal(1, version.Major);
    }
}
