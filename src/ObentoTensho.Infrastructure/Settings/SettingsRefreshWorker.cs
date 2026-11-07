using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObentoTensho.Core.Settings.Services;

namespace ObentoTensho.Infrastructure.Settings;

// BR-RPT-20: seed trước khi nhận request; sau đó làm mới mỗi 30 giây cho trường hợp nhiều instance
internal sealed partial class SettingsRefreshWorker(IServiceScopeFactory scopes, ILogger<SettingsRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SettingsSeeder>().SeedAsync(cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SettingsSeeder>().RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRefreshFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không làm mới được cài đặt, tiếp tục dùng bản đang có.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
