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

    public async Task<IReadOnlyList<Guid>> ListProcessingAsync(int limit, CancellationToken cancellationToken) =>
        await db.Images.AsNoTracking()
            .Where(i => i.Status == ImageStatus.Processing)
            .OrderBy(i => i.CreatedAtUtc)
            .Take(limit)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

    public void Add(StoredImage image) => db.Images.Add(image);
}
