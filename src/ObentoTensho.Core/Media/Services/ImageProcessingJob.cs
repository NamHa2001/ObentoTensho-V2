using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Media.Services;

// FR-IMG-08: sinh biến thể cho ảnh lớn đã tải lên — mỗi phút
public sealed class ImageProcessingJob(ImageService images) : IRecurringJob
{
    public string Name => "media.process-images";

    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public Task<int> RunAsync(CancellationToken cancellationToken) => images.ProcessPendingAsync(cancellationToken);
}
