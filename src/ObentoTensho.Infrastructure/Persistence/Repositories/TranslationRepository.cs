using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Localization.Abstractions;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class TranslationRepository(AppDbContext db) : ITranslationRepository
{
    public async Task<ContentTranslation?> FindAsync(TranslatableContent kind, int entityId, string lang, CancellationToken cancellationToken) =>
        await Set(kind).SingleOrDefaultAsync(t => t.EntityId == entityId && t.Lang == lang, cancellationToken);

    public async Task<IReadOnlyList<ContentTranslation>> ListForEntityAsync(TranslatableContent kind, int entityId, CancellationToken cancellationToken) =>
        await Set(kind).AsNoTracking().Where(t => t.EntityId == entityId).OrderBy(t => t.Lang).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentTranslation>> ListAsync(TranslatableContent kind, string lang, CancellationToken cancellationToken) =>
        await Set(kind).AsNoTracking().Where(t => t.Lang == lang).ToListAsync(cancellationToken);

    public void Add(ContentTranslation translation) => db.Add(translation);

    private IQueryable<ContentTranslation> Set(TranslatableContent kind) => kind switch
    {
        TranslatableContent.Promotion => db.PromotionTranslations,
        TranslatableContent.Banner => db.BannerTranslations,
        TranslatableContent.Gallery => db.GalleryTranslations,
        TranslatableContent.News => db.NewsTranslations,
        _ => db.StaticPageTranslations,
    };
}
