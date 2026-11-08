using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class ImageRepository(AppDbContext db) : IImageRepository
{
    public Task<StoredImage?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Images.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<StoredImage?> FindByHashAsync(string contentHash, CancellationToken cancellationToken) =>
        db.Images.AsNoTracking().OrderByDescending(i => i.CreatedAtUtc).FirstOrDefaultAsync(i => i.ContentHash == contentHash, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, StoredImage>> LoadAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Images.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);

    public Task<int> RequestRegenerationAsync(CancellationToken cancellationToken) =>
        db.Images.Where(i => i.Status == ImageStatus.Ready && !i.RegenerateRequested)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RegenerateRequested, true), cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListRegenerationAsync(int limit, CancellationToken cancellationToken) =>
        await db.Images.AsNoTracking()
            .Where(i => i.RegenerateRequested)
            .OrderBy(i => i.CreatedAtUtc)
            .Take(limit)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListProcessingAsync(int limit, CancellationToken cancellationToken) =>
        await db.Images.AsNoTracking()
            .Where(i => i.Status == ImageStatus.Processing)
            .OrderBy(i => i.CreatedAtUtc)
            .Take(limit)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StoredImage>> ListRetryableAsync(int maxAttempts, DateTime failedBeforeUtc, int limit, CancellationToken cancellationToken) =>
        await db.Images
            .Where(i => i.Status == ImageStatus.Failed && i.ProcessingAttempts < maxAttempts && i.FailedAtUtc != null && i.FailedAtUtc < failedBeforeUtc)
            .OrderBy(i => i.FailedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StoredImage>> ListUnlinkedAsync(DateTime createdBeforeUtc, int limit, CancellationToken cancellationToken)
    {
        // Cài đặt lưu mã ảnh dạng chuỗi (logo, QR chuyển khoản, ảnh chia sẻ mặc định)
        var settingIds = (await db.SystemSettings.AsNoTracking().Select(s => s.Value).ToListAsync(cancellationToken))
            .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToArray();
        return await db.Images.AsNoTracking()
            .Where(i => i.CreatedAtUtc < createdBeforeUtc
                && !settingIds.Contains(i.Id)
                && !db.Banners.Any(b => b.ImageId == i.Id || b.ImageMobileId == i.Id)
                && !db.Gallery.Any(g => g.ImageId == i.Id)
                && !db.News.Any(n => n.ThumbnailImageId == i.Id || n.OgImageId == i.Id)
                && !db.MenuItems.Any(m => m.MainImageId == i.Id)
                && !db.MenuItemImages.Any(m => m.ImageId == i.Id)
                && !db.MenuCategories.Any(c => c.ImageId == i.Id)
                && !db.Promotions.Any(p => p.BannerImageId == i.Id)
                && !db.Users.Any(u => u.AvatarImageId == i.Id))
            .OrderBy(i => i.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    // Đường dẫn ảnh trong HTML chứa mã ảnh dạng N (32 ký tự hex)
    public async Task<bool> IsUsedInHtmlAsync(Guid imageId, CancellationToken cancellationToken)
    {
        var id = imageId.ToString("N", CultureInfo.InvariantCulture);
        return await db.News.AnyAsync(n => n.ContentHtml.Contains(id), cancellationToken)
            || await db.StaticPages.AnyAsync(p => p.ContentHtml.Contains(id), cancellationToken)
            || await db.StaticPageVersions.AnyAsync(v => v.ContentHtml.Contains(id), cancellationToken)
            || await db.NewsletterCampaigns.AnyAsync(c => c.ContentHtml.Contains(id), cancellationToken)
            || await db.Promotions.AnyAsync(p => p.DescriptionHtml != null && p.DescriptionHtml.Contains(id), cancellationToken)
            || await db.MenuItems.AnyAsync(m => m.FullDescHtml != null && m.FullDescHtml.Contains(id), cancellationToken)
            || await db.MenuItemTranslations.AnyAsync(t => t.FullDescHtml != null && t.FullDescHtml.Contains(id), cancellationToken)
            || await db.NewsTranslations.AnyAsync(t => t.BodyHtml != null && t.BodyHtml.Contains(id), cancellationToken)
            || await db.StaticPageTranslations.AnyAsync(t => t.BodyHtml != null && t.BodyHtml.Contains(id), cancellationToken)
            || await db.PromotionTranslations.AnyAsync(t => t.BodyHtml != null && t.BodyHtml.Contains(id), cancellationToken);
    }

    public Task DeleteAsync(Guid imageId, CancellationToken cancellationToken) =>
        db.Images.Where(i => i.Id == imageId).ExecuteDeleteAsync(cancellationToken);

    public void Add(StoredImage image) => db.Images.Add(image);
}
