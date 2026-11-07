using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface INewsletterRepository
{
    Task<NewsletterSubscriber?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<NewsletterSubscriber?> FindByConfirmHashAsync(string hash, CancellationToken cancellationToken);

    Task<NewsletterSubscriber?> FindByUnsubscribeHashAsync(string hash, CancellationToken cancellationToken);

    void Add(NewsletterSubscriber subscriber);

    void Add(ConsentRecord consent);

    Task<(IReadOnlyList<NewsletterSubscriber> Items, int Total)> ListSubscribersAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsletterSubscriber>> ListForExportAsync(int limit, CancellationToken cancellationToken);

    Task<(IReadOnlyList<NewsletterCampaign> Items, int Total)> ListCampaignsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<NewsletterCampaign?> FindCampaignAsync(int id, CancellationToken cancellationToken);

    void Add(NewsletterCampaign campaign);

    void Remove(NewsletterCampaign campaign);

    Task<(int Sent, int Failed)> CountResultsAsync(int campaignId, CancellationToken cancellationToken);

    // Chiến dịch đang gửi dở, hoặc đã tới giờ gửi; cũ nhất trước
    Task<NewsletterCampaign?> FindDueCampaignAsync(DateTime nowUtc, CancellationToken cancellationToken);

    // Chốt danh sách người nhận: chỉ người còn hoạt động, đã xác nhận; chạy lại không thêm trùng. Trả tổng số người nhận
    Task<int> SnapshotRecipientsAsync(int campaignId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NewsletterSendItem>> TakePendingAsync(int campaignId, int count, CancellationToken cancellationToken);
}
