using NetVips;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using Image = NetVips.Image;

namespace ObentoTensho.Infrastructure.Media;

// D-04: libvips qua NetVips. SEC-10: giải mã thật để xác minh; chỉ nhận JPEG/PNG/WebP; bản lưu mã hóa lại nên mất EXIF/GPS.
// FR-IMG-13: tối đa 2 tác vụ đồng thời, giới hạn luồng và cache của libvips.
internal sealed class NetVipsImageProcessor : IImageProcessor
{
    private const int WebpQuality = 82;
    private const int BlurSize = 32;
    private const int UnboundedHeight = 10_000_000;

    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly string[] AllowedLoaders = ["VipsForeignLoadJpegBuffer", "VipsForeignLoadPngBuffer", "VipsForeignLoadWebpBuffer"];

    static NetVipsImageProcessor()
    {
        NetVips.NetVips.Concurrency = 2;
        Cache.Max = 0;
    }

    public Result<ProbedImage> Probe(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            if (!AllowedLoaders.Contains(Image.FindLoadBuffer(content), StringComparer.Ordinal))
            {
                return Result.Failure<ProbedImage>(ErrorCodes.FileInvalid);
            }

            using var header = Image.NewFromBuffer(content, access: Enums.Access.Sequential);
            if ((long)header.Width * header.Height > MediaRules.MaxPixels)
            {
                return Result.Failure<ProbedImage>(ErrorCodes.FileTooLarge);
            }

            // Giải mã thật một bản nhỏ: tệp cắt cụt hoặc giả mạo tiêu đề bị từ chối tại đây
            using var decoded = Image.ThumbnailBuffer(content, BlurSize);
            decoded.Avg();

            var rotated = header.Contains("orientation") && header.Get("orientation") is int and >= 5 and <= 8;
            return Result.Success(rotated ? new ProbedImage(header.Height, header.Width) : new ProbedImage(header.Width, header.Height));
        }
        catch (VipsException)
        {
            return Result.Failure<ProbedImage>(ErrorCodes.FileInvalid);
        }
    }

    public ProcessedImage Process(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Gate.Wait();
        try
        {
            using var original = Image.NewFromBuffer(content);
            using var upright = original.Autorot();
            var variants = MediaRules.VariantWidths(upright.Width).Select(w => new ImageVariant(w, Encode(content, w))).ToArray();
            return new ProcessedImage(upright.Width, upright.Height, BlurHash(content), variants);
        }
        finally
        {
            Gate.Release();
        }
    }

    // Luôn sinh từ ảnh gốc (FR-IMG-07); ThumbnailBuffer tự xoay đúng chiều và dùng lanczos3.
    // Mặc định nó co vào khung vuông width × width nên ảnh dọc bị nhỏ đi: chiều cao rất lớn để chỉ bề rộng quyết định.
    private static byte[] Encode(byte[] content, int width)
    {
        using var thumbnail = Image.ThumbnailBuffer(content, width, height: UnboundedHeight, size: Enums.Size.Down);
        return thumbnail.WebpsaveBuffer(q: WebpQuality, keep: Enums.ForeignKeep.None);
    }

    private static string BlurHash(byte[] content)
    {
        using var thumbnail = Image.ThumbnailBuffer(content, BlurSize);
        using var srgb = thumbnail.Colourspace(Enums.Interpretation.Srgb);
        using var flat = srgb.HasAlpha() ? srgb.Flatten(background: [255.0, 255.0, 255.0]) : srgb.Copy();
        using var rgb = flat.Cast(Enums.BandFormat.Uchar);
        return BlurHashEncoder.Encode(rgb.WriteToMemory<byte>(), rgb.Width, rgb.Height);
    }
}
