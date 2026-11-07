using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface IContactRepository
{
    Task<bool> ExistsSinceAsync(string email, string message, DateTime sinceUtc, CancellationToken cancellationToken);

    Task<ContactMessage?> FindAsync(int id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ContactMessage> Items, int Total)> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    void Add(ContactMessage message);
}
