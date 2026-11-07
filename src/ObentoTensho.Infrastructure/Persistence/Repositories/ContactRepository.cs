using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class ContactRepository(AppDbContext db) : IContactRepository
{
    public Task<bool> ExistsSinceAsync(string email, string message, DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.ContactMessages.AnyAsync(m => m.Email == email && m.CreatedAtUtc >= sinceUtc && m.Message == message, cancellationToken);

    public Task<ContactMessage?> FindAsync(int id, CancellationToken cancellationToken) =>
        db.ContactMessages.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<ContactMessage> Items, int Total)> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var source = db.ContactMessages.AsNoTracking().Where(m => !unreadOnly || !m.IsRead);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public void Add(ContactMessage message) => db.ContactMessages.Add(message);
}
