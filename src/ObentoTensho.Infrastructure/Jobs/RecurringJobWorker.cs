using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Infrastructure.Jobs;

// docs/srs/20 §4: chạy các IRecurringJob theo chu kỳ, ghi log đầu/cuối và số bản ghi, không để một việc lỗi làm dừng việc khác.
// Thay cho Hangfire ở Phase 1: không cần tạo bảng lúc chạy (tài khoản CSDL không có quyền DDL — DB-10), không có dashboard phải bảo vệ.
internal sealed partial class RecurringJobWorker(
    IServiceScopeFactory scopes,
    SqlJobLock jobLock,
    IOptions<JobOptions> options,
    IClock clock,
    TimeProvider timeProvider,
    ILogger<RecurringJobWorker> logger) : BackgroundService
{
    private readonly Dictionary<string, DateTime> _lastRun = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var tick = TimeSpan.FromSeconds(options.Value.TickSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunDueJobsAsync(stoppingToken);
            await Task.Delay(tick, timeProvider, stoppingToken);
        }
    }

    private async Task RunDueJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        foreach (var job in scope.ServiceProvider.GetServices<IRecurringJob>())
        {
            var now = clock.UtcNow;
            if (_lastRun.TryGetValue(job.Name, out var last) && now - last < job.Interval)
            {
                continue;
            }

            _lastRun[job.Name] = now;
            await RunAsync(job, scope.ServiceProvider, cancellationToken);
        }
    }

    private async Task RunAsync(IRecurringJob job, IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            await using var held = await jobLock.TryAcquireAsync(job.Name, cancellationToken);
            if (held is null)
            {
                return;
            }

            var daily = job as IDailyJob;
            var runs = services.GetRequiredService<IJobRunStore>();
            var today = clock.TodayLocal;
            if (daily is not null && !await IsDailyDueAsync(daily, runs, today, cancellationToken))
            {
                return;
            }

            LogStarted(logger, job.Name);
            var processed = await job.RunAsync(cancellationToken);
            if (daily is not null)
            {
                await runs.RecordAsync(job.Name, today, clock.UtcNow, cancellationToken);
            }

            LogFinished(logger, job.Name, processed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, job.Name, ex);
        }
    }

    // Đã tới giờ (giờ VN) và hôm nay chưa chạy xong; lần trước lỗi thì chưa ghi ngày nên lượt sau thử lại
    private async Task<bool> IsDailyDueAsync(IDailyJob job, IJobRunStore runs, DateOnly today, CancellationToken cancellationToken)
    {
        var nowLocal = TimeOnly.FromDateTime(clock.ToLocal(clock.UtcNow));
        return nowLocal >= job.RunAtLocal && DailySchedule.IsDue(nowLocal, job.RunAtLocal, today, await runs.LastRunDateAsync(job.Name, cancellationToken));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bắt đầu việc định kỳ {Job}.")]
    private static partial void LogStarted(ILogger logger, string job);

    [LoggerMessage(Level = LogLevel.Information, Message = "Xong việc định kỳ {Job}: {Processed} bản ghi.")]
    private static partial void LogFinished(ILogger logger, string job, int processed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Việc định kỳ {Job} lỗi; sẽ chạy lại ở lượt sau.")]
    private static partial void LogFailed(ILogger logger, string job, Exception exception);
}
