namespace ObentoTensho.Core.Media.Abstractions;

// Tệp ảnh nằm ngoài CSDL: ảnh gốc không công khai, biến thể phục vụ qua đường dẫn tĩnh (SEC-56)
public interface IImageFileStore
{
    // Trả đường dẫn tương đối để lưu vào OriginalPath
    Task<string> SaveOriginalAsync(Guid imageId, byte[] content, CancellationToken cancellationToken);

    Task<byte[]> ReadOriginalAsync(string originalPath, CancellationToken cancellationToken);

    Task SaveVariantAsync(Guid imageId, int version, ImageVariant variant, CancellationToken cancellationToken);

    string VariantUrl(Guid imageId, int version, int width, string format);
}
