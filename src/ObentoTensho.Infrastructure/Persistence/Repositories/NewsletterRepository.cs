using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class NewsletterRepository(AppDbContext db) : INewsletterRepository
{
    public Task<NewsletterSubscriber?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.NewsletterSubscribers.SingleOrDefaultAsync(s => s.Email == email, cancellationToken);

    public Task<NewsletterSubscriber?> FindByConfirmHashAsync(string hash, CancellationToken cancellationToken) =>
        db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.ConfirmTokenHash == hash, cancellationToken);

    public Task<NewsletterSubscriber?> FindByUnsubscribeHashAsync(string hash, CancellationToken cancellationToken) =>
        db.NewsletterSubscribers.SingleOrDefaultAsync(s => s.UnsubscribeTokenHash == hash, cancellationToken);

    public void Add(NewsletterSubscriber subscriber) => db.NewsletterSubscribers.Add(subscriber);

    public void Add(ConsentRecord consent) => db.ConsentRecords.Add(consent);

    public async Task<(IReadOnlyList<NewsletterSubscriber> Items, int Total)> ListSubscribersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await db.NewsletterSubscribers.CountAsync(cancellationToken);
        var items = await db.NewsletterSubscribers.AsNoTracking().OrderByDescending(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<NewsletterSubscriber>> ListForExportAsync(int limit, CancellationToken cancellationToken) =>
        await db.NewsletterSubscribers.AsNoTracking().OrderBy(s => s.Id).Take(limit).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<NewsletterCampaign> Items, int Total)> ListCampaignsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await db.NewsletterCampaigns.CountAsync(cancellationToken);
        var items = await db.NewsletterCampaigns.AsNoTracking().OrderByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<NewsletterCampaign?> FindCampaignAsync(int id, CancellationToken cancellationToken) =>
        db.NewsletterCampaigns.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(NewsletterCampaign campaign) => db.NewsletterCampaigns.Add(campaign);

    public void Remove(NewsletterCampaign campaign) => db.NewsletterCampaigns.Remove(campaign);

    public async Task<(int Sent, int Failed)> CountResultsAsync(int campaignId, CancellationToken cancellationToken)
    {
        var counts = await db.NewsletterRecipients.AsNoTracking()
            .Where(r => r.CampaignId == campaignId)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return (counts.Where(c => c.Key == NewsletterRecipientStatus.Sent).Sum(c => c.Count), counts.Where(c => c.Key == NewsletterRecipientStatus.Failed).Sum(c => c.Count));
    }

    public Task<NewsletterCampaign?> FindDueCampaignAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
        db.NewsletterCampaigns
            .Where(c => c.Status == NewsletterCampaignStatus.Sending || (c.Status == NewsletterCampaignStatus.Scheduled && c.ScheduledAtUtc <= nowUtc))
            .OrderByDescending(c => c.Status)
            .ThenBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> SnapshotRecipientsAsync(int campaignId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO [NewsletterRecipients] ([CampaignId], [SubscriberId], [Status], [Attempts])
            SELECT {campaignId}, s.[Id], 0, 0
            FROM [NewsletterSubscribers] s
            WHERE s.[IsActive] = 1 AND s.[ConfirmedAtUtc] IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM [NewsletterRecipients] r WHERE r.[CampaignId] = {campaignId} AND r.[SubscriberId] = s.[Id])
            """, cancellationToken);
        return await db.NewsletterRecipients.CountAsync(r => r.CampaignId == campaignId, cancellationToken);
    }

    public async Task<IReadOnlyList<NewsletterSendItem>> TakePendingAsync(int campaignId, int count, CancellationToken cancellationToken)
    {
        var rows = await db.NewsletterRecipients
            .Where(r => r.CampaignId == campaignId && r.Status == NewsletterRecipientStatus.Pending)
            .OrderBy(r => r.SubscriberId)
            .Take(count)
            .Join(db.NewsletterSubscribers, r => r.SubscriberId, s => s.Id, (r, s) => new { Recipient = r, Subscriber = s })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new NewsletterSendItem(x.Recipient, x.Subscriber)).ToArray();
    }
}
