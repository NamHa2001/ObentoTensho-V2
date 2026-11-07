using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

// Banner/gallery đang bật kèm ảnh; ngày hiển thị lọc lúc đọc nên cache không làm sai lịch hiển thị
public sealed record PublicContentSnapshot(
    IReadOnlyList<Banner> Banners,
    IReadOnlyList<GalleryItem> Gallery,
    IReadOnlyDictionary<Guid, StoredImage> Images);
