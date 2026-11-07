using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface IContentRepository
{
    Task<IReadOnlyList<Banner>> ListBannersAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<Banner?> FindBannerAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Banner>> FindBannersAsync(CancellationToken cancellationToken);

    void AddBanner(Banner banner);

    void RemoveBanner(Banner banner);

    Task<IReadOnlyList<GalleryItem>> ListGalleryAsync(bool activeOnly, GalleryCategory? category, CancellationToken cancellationToken);

    Task<GalleryItem?> FindGalleryItemAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<GalleryItem>> FindGalleryItemsAsync(CancellationToken cancellationToken);

    void AddGalleryItem(GalleryItem item);

    void RemoveGalleryItem(GalleryItem item);
}
