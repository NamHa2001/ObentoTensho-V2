using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Media.Services;

// FR-IMG-08: ảnh xử lý nền bị lỗi được đưa lại hàng xử lý tối đa 3 lần, mỗi lần cách 5 phút; hết lượt thì chờ người bấm "Thử lại"
public sealed class ImageRetryJob(IImageRepository images, IUnitOfWork unitOfWork, IClock clock) : IRecurringJob
{
    private const int BatchSize = 20;

    public string Name => "media.retry-failed";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var failed = await images.ListRetryableAsync(MediaRules.MaxProcessingAttempts, clock.UtcNow - MediaRules.RetryDelay, BatchSize, cancellationToken);
        foreach (var image in failed)
        {
            image.Status = Contracts.Enums.ImageStatus.Processing;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return failed.Count;
    }
}
