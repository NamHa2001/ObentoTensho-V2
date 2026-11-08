using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class SlugRedirectRepository(AppDbContext db) : ISlugRedirectRepository
{
    public Task<SlugRedirect?> FindAsync(string entityType, string oldSlug, CancellationToken cancellationToken) =>
        db.SlugRedirects.AsNoTracking().SingleOrDefaultAsync(r => r.EntityType == entityType && r.OldSlug == oldSlug, cancellationToken);

    public Task RemoveAsync(string entityType, string slug, CancellationToken cancellationToken) =>
        db.SlugRedirects.Where(r => r.EntityType == entityType && r.OldSlug == slug).ExecuteDeleteAsync(cancellationToken);

    public void Add(SlugRedirect redirect) => db.SlugRedirects.Add(redirect);
}
