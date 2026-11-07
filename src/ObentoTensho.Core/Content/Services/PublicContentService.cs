using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// Banner, gallery công khai: chỉ mục đang bật có ảnh sẵn sàng; banner lọc theo ngày VN ngay lúc đọc (BR-CNT-34)
public sealed class PublicContentService(
    IContentRepository content,
    IImageRepository images,
    ImageMapper imageMapper,
    IPublicContentCache cache,
    IClock clock)
{
    public async Task<IReadOnlyList<PublicBannerDto>> BannersAsync(CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        var today = clock.TodayLocal;
        return snapshot.Banners
            .Where(b => b.IsVisible(today) && Ready(snapshot, b.ImageId))
            .Select(b => new PublicBannerDto(
                b.Id,
                imageMapper.ToDto(snapshot.Images[b.ImageId]),
                b.ImageMobileId is { } m && Ready(snapshot, m) ? imageMapper.ToDto(snapshot.Images[m]) : null,
                b.Title,
                b.Subtitle,
                b.CtaText,
                b.CtaLink))
            .ToArray();
    }

    public async Task<IReadOnlyList<PublicGalleryItemDto>> GalleryAsync(GalleryCategory? category, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        return snapshot.Gallery
            .Where(i => (category is null || i.Category == category) && (i.MediaType == GalleryMediaType.Video || (i.ImageId is { } id && Ready(snapshot, id))))
            .Select(i => new PublicGalleryItemDto(
                i.Id,
                i.MediaType,
                i.ImageId is { } id ? imageMapper.ToDto(snapshot.Images[id]) : null,
                SafeLinks.VideoEmbedUrl(i.VideoUrl),
                i.Category,
                i.Caption))
            .ToArray();
    }

    private static bool Ready(PublicContentSnapshot snapshot, Guid imageId) =>
        snapshot.Images.TryGetValue(imageId, out var image) && image.Status == ImageStatus.Ready;

    private Task<PublicContentSnapshot> SnapshotAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(LoadAsync, cancellationToken);

    private async Task<PublicContentSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var banners = await content.ListBannersAsync(activeOnly: true, cancellationToken);
        var gallery = await content.ListGalleryAsync(activeOnly: true, category: null, cancellationToken);
        var ids = banners.SelectMany(b => new[] { b.ImageId, b.ImageMobileId ?? b.ImageId })
            .Concat(gallery.Select(g => g.ImageId).OfType<Guid>())
            .Distinct()
            .ToArray();
        return new PublicContentSnapshot(banners, gallery, await images.LoadAsync(ids, cancellationToken));
    }
}
