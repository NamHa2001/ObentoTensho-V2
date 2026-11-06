using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ObentoTensho.Infrastructure.Messaging;

// PERF-53: thăm dò thích ứng — có việc thì làm liên tục, rảnh thì giãn dần tới 10 giây
internal sealed partial class OutboxWorker(
    OutboxProcessor processor,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var delay = MinDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await processor.ProcessBatchAsync(stoppingToken);
                delay = processed > 0 ? TimeSpan.Zero : Next(delay);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Lỗi CSDL tạm thời: ghi log và thử lại sau, không để worker dừng hẳn
                LogLoopError(logger, ex);
                delay = MaxDelay;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
        }
    }

    private static TimeSpan Next(TimeSpan current)
    {
        var doubled = current == TimeSpan.Zero ? MinDelay : current * 2;
        return doubled > MaxDelay ? MaxDelay : doubled;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Lỗi khi xử lý hộp thư đi; sẽ thử lại.")]
    private static partial void LogLoopError(ILogger logger, Exception exception);
}
