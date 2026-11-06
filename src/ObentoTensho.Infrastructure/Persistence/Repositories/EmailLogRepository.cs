using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class EmailLogRepository(AppDbContext db) : IEmailLogRepository
{
    public Task<EmailLog?> FindForUpdateAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.EmailLogs.SingleOrDefaultAsync(l => l.IdempotencyKey == idempotencyKey, cancellationToken);

    public void Add(EmailLog log) => db.EmailLogs.Add(log);

    public Task<int> CountSentToAsync(string toEmail, IReadOnlyCollection<string>? types, DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.EmailLogs.CountAsync(
            l => l.ToEmail == toEmail && l.Status == EmailStatus.Sent && l.SentAtUtc >= sinceUtc && (types == null || types.Contains(l.Type)),
            cancellationToken);

    public Task<int> CountSentSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.EmailLogs.CountAsync(l => l.Status == EmailStatus.Sent && l.SentAtUtc >= sinceUtc, cancellationToken);
}
