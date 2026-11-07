using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class ContentRepository(AppDbContext db) : IContentRepository
{
    public async Task<IReadOnlyList<Banner>> ListBannersAsync(bool activeOnly, CancellationToken cancellationToken) =>
        await db.Banners.AsNoTracking()
            .Where(b => !activeOnly || b.IsActive)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

    public Task<Banner?> FindBannerAsync(int id, CancellationToken cancellationToken) =>
        db.Banners.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Banner>> FindBannersAsync(CancellationToken cancellationToken) =>
        await db.Banners.ToListAsync(cancellationToken);

    public void AddBanner(Banner banner) => db.Banners.Add(banner);

    public void RemoveBanner(Banner banner) => db.Banners.Remove(banner);

    public async Task<IReadOnlyList<GalleryItem>> ListGalleryAsync(bool activeOnly, GalleryCategory? category, CancellationToken cancellationToken) =>
        await db.Gallery.AsNoTracking()
            .Where(g => (!activeOnly || g.IsActive) && (category == null || g.Category == category))
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

    public Task<GalleryItem?> FindGalleryItemAsync(int id, CancellationToken cancellationToken) =>
        db.Gallery.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GalleryItem>> FindGalleryItemsAsync(CancellationToken cancellationToken) =>
        await db.Gallery.ToListAsync(cancellationToken);

    public void AddGalleryItem(GalleryItem item) => db.Gallery.Add(item);

    public void RemoveGalleryItem(GalleryItem item) => db.Gallery.Remove(item);
}
