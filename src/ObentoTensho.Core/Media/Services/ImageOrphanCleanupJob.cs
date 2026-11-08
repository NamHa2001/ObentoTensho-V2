using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Media.Services;

// BR-CNT-32, FR-IMG-10, docs/srs/20 §4: Chủ nhật 03:15 giờ VN xóa ảnh tải lên quá 30 ngày mà không còn chỗ nào dùng
// (cột tham chiếu, cài đặt, hay đường dẫn trong nội dung HTML) — xóa cả ảnh gốc lẫn biến thể
public sealed class ImageOrphanCleanupJob(IImageRepository images, IImageFileStore files, IClock clock) : IDailyJob
{
    private const int BatchSize = 200;

    public string Name => "media.orphan-cleanup";

    public TimeOnly RunAtLocal => new(3, 15);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (clock.TodayLocal.DayOfWeek != DayOfWeek.Sunday)
        {
            return 0;
        }

        return await CleanAsync(cancellationToken);
    }

    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var image in await images.ListUnlinkedAsync(clock.UtcNow - MediaRules.OrphanGrace, BatchSize, cancellationToken))
        {
            if (await images.IsUsedInHtmlAsync(image.Id, cancellationToken))
            {
                continue;
            }

            await images.DeleteAsync(image.Id, cancellationToken);
            await files.DeleteAsync(image.Id, image.OriginalPath, cancellationToken);
            deleted++;
        }

        return deleted;
    }
}
