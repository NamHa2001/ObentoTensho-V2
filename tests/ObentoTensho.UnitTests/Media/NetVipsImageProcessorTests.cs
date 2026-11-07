using NetVips;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Infrastructure.Media;
using Image = NetVips.Image;

namespace ObentoTensho.UnitTests.Media;

// docs/srs/04 §2, SEC-10: chỉ nhận ảnh thật, xoay đúng chiều, không phóng to, bỏ siêu dữ liệu, chặn bom giải nén
public sealed class NetVipsImageProcessorTests
{
    private readonly NetVipsImageProcessor _processor = new();

    private static byte[] Jpeg(int width, int height, int orientation = 1)
    {
        using var noise = Image.Gaussnoise(width, height, mean: 128, sigma: 40);
        using var band = noise.Cast(Enums.BandFormat.Uchar);
        using var rgb = band.Bandjoin(band, band);
        using var srgb = rgb.Copy(interpretation: Enums.Interpretation.Srgb);
        using var tagged = srgb.Mutate(m => m.Set(GValue.GIntType, "orientation", orientation));
        return tagged.JpegsaveBuffer(q: 80);
    }

    [Fact]
    public void Sinh_bien_the_khong_phong_to_va_co_blurhash()
    {
        var result = _processor.Process(Jpeg(1200, 800));

        Assert.Equal([320, 640, 1024], result.Variants.Select(v => v.Width));
        Assert.Equal((1200, 800, 28), (result.Width, result.Height, result.BlurHash.Length));
        foreach (var variant in result.Variants)
        {
            using var image = Image.NewFromBuffer(variant.Content);
            Assert.Equal(variant.Width, image.Width);
            Assert.StartsWith("webp", image.Get("vips-loader").ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Anh_nho_hon_320_giu_dung_be_rong_goc() =>
        Assert.Equal([200], _processor.Process(Jpeg(200, 100)).Variants.Select(v => v.Width));

    [Fact]
    public void Anh_dien_thoai_xoay_dung_chieu_va_mat_exif()
    {
        var jpeg = Jpeg(800, 400, orientation: 6);

        var probe = _processor.Probe(jpeg);
        var result = _processor.Process(jpeg);

        Assert.Equal((400, 800), (probe.Value.Width, probe.Value.Height));
        Assert.Equal((400, 800), (result.Width, result.Height));
        using var variant = Image.NewFromBuffer(result.Variants[0].Content);
        Assert.Equal(320, variant.Width);
        Assert.Equal(640, variant.Height);
        Assert.False(variant.Contains("exif-data"));
    }

    [Fact]
    public void Nhan_png_va_webp()
    {
        using var noise = Image.Black(100, 100, bands: 3).Copy(interpretation: Enums.Interpretation.Srgb);

        Assert.True(_processor.Probe(noise.PngsaveBuffer()).IsSuccess);
        Assert.True(_processor.Probe(noise.WebpsaveBuffer()).IsSuccess);
    }

    [Fact]
    public void Tu_choi_tep_khong_phai_anh_dinh_dang_la_va_anh_cat_cut()
    {
        using var gif = Image.Black(50, 50);
        var jpeg = Jpeg(1000, 1000);

        Assert.Equal(ErrorCodes.FileInvalid, _processor.Probe("<svg onload=alert(1)></svg>"u8.ToArray()).ErrorCode);
        Assert.Equal(ErrorCodes.FileInvalid, _processor.Probe("MZ khong phai anh"u8.ToArray()).ErrorCode);
        Assert.Equal(ErrorCodes.FileInvalid, _processor.Probe(gif.GifsaveBuffer()).ErrorCode);
        Assert.Equal(ErrorCodes.FileInvalid, _processor.Probe(jpeg[..200]).ErrorCode);
    }

    [Fact]
    public void Anh_qua_40_megapixel_bi_tu_choi_truoc_khi_giai_ma_het()
    {
        // 7.000 × 6.000 = 42 MP nhưng tệp PNG chỉ vài chục KB
        using var bomb = Image.Black(7000, 6000);

        Assert.Equal(ErrorCodes.FileTooLarge, _processor.Probe(bomb.PngsaveBuffer(compression: 9)).ErrorCode);
    }

    [Theory]
    [InlineData(2400, new[] { 320, 640, 1024, 1600, 2400 })]
    [InlineData(3000, new[] { 320, 640, 1024, 1600, 2400 })]
    [InlineData(320, new[] { 320 })]
    [InlineData(100, new[] { 100 })]
    public void Be_rong_bien_the(int width, int[] expected) =>
        Assert.Equal(expected, MediaRules.VariantWidths(width));
}
