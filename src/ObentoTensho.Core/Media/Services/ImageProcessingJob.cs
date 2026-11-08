using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Menu.Abstractions;

namespace ObentoTensho.Core.Media.Services;

// FR-IMG-08: sinh biến thể cho ảnh lớn đã tải lên — mỗi phút. Có ảnh vừa xong thì xóa cache trang khách để ảnh hiện ngay
public sealed class ImageProcessingJob(ImageService images, IMenuCache menuCache, IPublicContentCache contentCache) : IRecurringJob
{
    public string Name => "media.process-images";

    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var processed = await images.ProcessPendingAsync(cancellationToken);
        if (processed > 0)
        {
            menuCache.Invalidate();
            contentCache.Invalidate();
        }

        return processed;
    }
}
