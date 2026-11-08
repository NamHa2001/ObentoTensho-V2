using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-42: xóa hẳn bài nằm thùng rác quá 30 ngày — 03:00 giờ VN
public sealed class NewsTrashPurgeJob(NewsAdminService news) : IDailyJob
{
    public string Name => "content.news-trash-purge";

    public TimeOnly RunAtLocal => new(3, 0);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public Task<int> RunAsync(CancellationToken cancellationToken) => news.PurgeTrashAsync(cancellationToken);
}
