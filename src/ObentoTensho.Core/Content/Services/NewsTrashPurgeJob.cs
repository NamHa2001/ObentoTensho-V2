using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-42: xóa hẳn bài nằm thùng rác quá 30 ngày — mỗi ngày
public sealed class NewsTrashPurgeJob(NewsAdminService news) : IRecurringJob
{
    public string Name => "content.news-trash-purge";

    public TimeSpan Interval => TimeSpan.FromDays(1);

    public Task<int> RunAsync(CancellationToken cancellationToken) => news.PurgeTrashAsync(cancellationToken);
}
