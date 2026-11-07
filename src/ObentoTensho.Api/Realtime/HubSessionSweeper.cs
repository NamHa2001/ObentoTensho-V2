using ObentoTensho.Core.Auth.Services;

namespace ObentoTensho.Api.Realtime;

// SEC-69: ngắt kết nối khi access token gốc hết hạn hoặc phiên bị thu hồi (đổi mật khẩu, khóa, đổi vai trò — `sv` đổi)
public sealed partial class HubSessionSweeper(
    HubConnectionTracker tracker,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<HubSessionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var connections = tracker.Authenticated();
        if (connections.Count == 0)
        {
            return 0;
        }

        await using var scope = scopes.CreateAsyncScope();
        var validator = scope.ServiceProvider.GetRequiredService<AccessTokenSessionValidator>();
        var now = time.GetUtcNow();
        var closed = 0;
        foreach (var (context, identity) in connections)
        {
            var valid = identity.ExpiresAt > now && (identity.IsStaff
                ? await validator.IsStaffSessionValidAsync(identity.Id, identity.SecurityStamp, cancellationToken)
                : await validator.IsCustomerSessionValidAsync(identity.Id, identity.SecurityStamp, cancellationToken));
            if (!valid)
            {
                context.Abort();
                closed++;
            }
        }

        return closed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Kiểm phiên kết nối realtime lỗi; sẽ kiểm lại ở lượt sau.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
