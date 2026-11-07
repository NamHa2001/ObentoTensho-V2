using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Core.Media.Abstractions;

public interface IImageRepository
{
    Task<StoredImage?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<StoredImage?> FindByHashAsync(string contentHash, CancellationToken cancellationToken);

    // Không theo dõi, cho DTO
    Task<IReadOnlyDictionary<Guid, StoredImage>> LoadAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListProcessingAsync(int limit, CancellationToken cancellationToken);

    void Add(StoredImage image);
}
