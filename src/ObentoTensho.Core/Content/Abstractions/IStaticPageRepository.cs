using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface IStaticPageRepository
{
    Task<IReadOnlyList<StaticPage>> ListAsync(CancellationToken cancellationToken);

    Task<StaticPage?> FindAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaticPageVersion>> ListVersionsAsync(int pageId, CancellationToken cancellationToken);

    Task<StaticPageVersion?> FindVersionAsync(int pageId, int version, CancellationToken cancellationToken);

    void Add(StaticPage page);

    void AddVersion(StaticPageVersion version);
}
