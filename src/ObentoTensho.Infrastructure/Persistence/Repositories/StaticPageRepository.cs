using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class StaticPageRepository(AppDbContext db) : IStaticPageRepository
{
    public async Task<IReadOnlyList<StaticPage>> ListAsync(CancellationToken cancellationToken) =>
        await db.StaticPages.AsNoTracking().ToListAsync(cancellationToken);

    public Task<StaticPage?> FindAsync(string slug, CancellationToken cancellationToken) =>
        db.StaticPages.SingleOrDefaultAsync(p => p.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<StaticPageVersion>> ListVersionsAsync(int pageId, CancellationToken cancellationToken) =>
        await db.StaticPageVersions.AsNoTracking().Where(v => v.PageId == pageId).OrderByDescending(v => v.Version).ToListAsync(cancellationToken);

    public Task<StaticPageVersion?> FindVersionAsync(int pageId, int version, CancellationToken cancellationToken) =>
        db.StaticPageVersions.AsNoTracking().SingleOrDefaultAsync(v => v.PageId == pageId && v.Version == version, cancellationToken);

    public void Add(StaticPage page) => db.StaticPages.Add(page);

    public void AddVersion(StaticPageVersion version) => db.StaticPageVersions.Add(version);
}
