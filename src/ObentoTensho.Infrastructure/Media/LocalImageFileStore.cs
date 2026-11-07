using System.Globalization;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Media.Abstractions;

namespace ObentoTensho.Infrastructure.Media;

// Tên tệp sinh từ id, không dùng tên khách gửi (SEC-10). Ảnh gốc nằm ngoài thư mục phục vụ công khai (FR-IMG-01)
internal sealed class LocalImageFileStore(IOptions<MediaOptions> options) : IImageFileStore
{
    private const string Folder = "images";

    public async Task<string> SaveOriginalAsync(Guid imageId, byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.Value.OriginalsDirectory);
        var name = imageId.ToString("N", CultureInfo.InvariantCulture);
        await File.WriteAllBytesAsync(Path.Combine(options.Value.OriginalsDirectory, name), content, cancellationToken);
        return name;
    }

    public Task<byte[]> ReadOriginalAsync(string originalPath, CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(Path.Combine(options.Value.OriginalsDirectory, Path.GetFileName(originalPath)), cancellationToken);

    public async Task SaveVariantAsync(Guid imageId, int version, ImageVariant variant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variant);
        var directory = Path.Combine(options.Value.PublicDirectory, Folder, imageId.ToString("N", CultureInfo.InvariantCulture), version.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, $"{variant.Width}.webp"), variant.Content, cancellationToken);
    }

    public string VariantUrl(Guid imageId, int version, int width, string format) =>
        string.Create(CultureInfo.InvariantCulture, $"{options.Value.PublicBaseUrl.TrimEnd('/')}/{Folder}/{imageId:N}/{version}/{width}.{format}");
}
