using System.Net;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Api.Configuration;

// docs/srs/02: /health/live (tiến trình còn chạy) công khai; /health/ready kiểm SQL và hàng thư đi, chỉ mở cho mạng nội bộ
// (bộ cân bằng tải, giám sát) vì lộ trạng thái hạ tầng
public static class HealthConfiguration
{
    public const string ReadyTag = "ready";
    private static readonly TimeSpan OutboxStaleAfter = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SqlHealthCheck>("sql", tags: [ReadyTag])
            .AddCheck<OutboxHealthCheck>("outbox", tags: [ReadyTag]);
        return services;
    }

    public static WebApplication MapAppHealthChecks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) })
            .AllowAnonymous()
            .AddEndpointFilter(async (context, next) =>
                IsInternal(context.HttpContext.Connection.RemoteIpAddress)
                    ? await next(context)
                    : Results.Problem(ApiProblems.Create(context.HttpContext, ErrorCodes.NotFound)));
        return app;
    }

    // Loopback hoặc dải IP riêng (10/8, 172.16/12, 192.168/16, IPv6 nội bộ)
    public static bool IsInternal(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal)
        {
            return true;
        }

        var b = address.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168));
    }

    private sealed class SqlHealthCheck(AppDbContext db) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Không kết nối được SQL Server.");
    }

    // Thư chờ gửi quá 15 phút ⇒ hàng đợi đang kẹt (SMTP lỗi hoặc worker dừng)
    private sealed class OutboxHealthCheck(AppDbContext db, TimeProvider time) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var stale = time.GetUtcNow().UtcDateTime - OutboxStaleAfter;
            var stuck = await db.OutboxMessages.AnyAsync(m => m.ProcessedAtUtc == null && m.FailedAtUtc == null && m.NextAttemptAtUtc < stale, cancellationToken);
            return stuck ? HealthCheckResult.Degraded("Hàng thư đi có thư chờ quá 15 phút.") : HealthCheckResult.Healthy();
        }
    }
}
