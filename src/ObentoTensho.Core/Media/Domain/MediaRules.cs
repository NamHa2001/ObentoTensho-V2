namespace ObentoTensho.Core.Media.Domain;

// docs/srs/04 §2: giới hạn upload, các bề rộng biến thể
public static class MediaRules
{
    public const long MaxUploadBytes = 15 * 1024 * 1024;
    public const long MaxPixels = 40_000_000;

    // FR-IMG-08: tới mức này xử lý ngay trong request, lớn hơn thì xử lý nền
    public const long SyncMaxPixels = 12_000_000;
    public const string VariantFormat = "webp";
    public const int AltTextMaxLength = 200;

    private static readonly int[] Sizes = [320, 640, 1024, 1600, 2400];

    // FR-IMG-03: không phóng to; ảnh nhỏ hơn 320 px giữ một biến thể đúng bề rộng gốc
    public static IReadOnlyList<int> VariantWidths(int originalWidth)
    {
        var widths = Sizes.Where(w => w <= originalWidth).ToArray();
        return widths.Length > 0 ? widths : [originalWidth];
    }
}
