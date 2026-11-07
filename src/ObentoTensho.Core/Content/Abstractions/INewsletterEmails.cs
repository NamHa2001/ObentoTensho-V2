using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface INewsletterEmails
{
    // Thư xác nhận, ghi vào hộp thư đi cùng transaction
    void EnqueueConfirmation(NewsletterSubscriber subscriber, string token);

    // Thư bản tin có liên kết hủy và header one-click (BR-CNT-24, BR-CNT-26); job gửi trực tiếp để ghi trạng thái từng người nhận
    EmailRequest BuildCampaignEmail(NewsletterCampaign campaign, NewsletterSubscriber subscriber, string unsubscribeToken);
}
