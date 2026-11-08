using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Maintenance.Services;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Verification.Domain;
using ObentoTensho.Infrastructure.Messaging;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Maintenance;

// DB-13 (dọn dữ liệu theo vòng đời), TIME-08 (việc hằng ngày theo giờ VN), docs/srs/02 (/health/ready)
[Collection(SqlServerFixture.CollectionName)]
public sealed class MaintenanceTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;

    public MaintenanceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static OutboxMessage Outbox(string key, DateTime at, bool processed, bool failed = false) => new()
    {
        Type = "admin_alert", PayloadJson = "{}", IdempotencyKey = key, CreatedAtUtc = at, NextAttemptAtUtc = at,
        ProcessedAtUtc = processed ? at : null, FailedAtUtc = failed ? at : null,
    };

    [Fact]
    public async Task Don_du_lieu_qua_han_giu_du_lieu_con_han_va_tai_khoan_co_lien_quan()
    {
        var tag = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var oldUnverified = AuthApiClient.NewEmail();
        var oldWithOrder = AuthApiClient.NewEmail();
        var recentUnverified = AuthApiClient.NewEmail();
        var oldVerified = AuthApiClient.NewEmail();
        var auth = new AuthApiClient(_http);
        foreach (var email in new[] { oldUnverified, oldWithOrder, recentUnverified, oldVerified })
        {
            await auth.RegisterAsync(email);
        }

        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var customerToken = await AuthApiClient.ReadAccessTokenAsync(await auth.LoginAsync(oldWithOrder));
        Assert.Equal(HttpStatusCode.Created, (await new OrderApiClient(_http).CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)]), customerToken)).StatusCode);

        await using (var db = _fixture.CreateContext())
        {
            db.OutboxMessages.AddRange(
                Outbox($"old-done:{tag}", now.AddDays(-8), processed: true),
                Outbox($"new-done:{tag}", now.AddDays(-6), processed: true),
                Outbox($"old-pending:{tag}", now.AddDays(-40), processed: false),
                Outbox($"failed-20:{tag}", now.AddDays(-20), processed: false, failed: true),
                Outbox($"failed-31:{tag}", now.AddDays(-31), processed: false, failed: true));
            db.OtpChallenges.AddRange(
                new OtpChallenge { Purpose = OtpPurpose.ReservationContact, TargetHash = $"old-{tag}", CodeHash = "x", ExpiresAtUtc = now.AddDays(-2), CreatedAtUtc = now.AddDays(-2) },
                new OtpChallenge { Purpose = OtpPurpose.ReservationContact, TargetHash = $"new-{tag}", CodeHash = "x", ExpiresAtUtc = now, CreatedAtUtc = now.AddHours(-2) });
            db.EmailLogs.AddRange(
                new EmailLog { Type = "test", ToEmail = "a@example.com", IdempotencyKey = $"old-{tag}", CreatedAtUtc = now.AddDays(-91) },
                new EmailLog { Type = "test", ToEmail = "a@example.com", IdempotencyKey = $"new-{tag}", CreatedAtUtc = now.AddDays(-89) });
            db.ContactMessages.AddRange(
                new ContactMessage { FullName = "Cũ", Email = $"old-{tag}@example.com", Message = "x", CreatedAtUtc = now.AddDays(-731) },
                new ContactMessage { FullName = "Mới", Email = $"new-{tag}@example.com", Message = "x", CreatedAtUtc = now.AddDays(-30) });
            await db.SaveChangesAsync();
            await db.Users.Where(u => u.Email == oldUnverified || u.Email == oldWithOrder)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAtUtc, now.AddDays(-31)).SetProperty(u => u.IsEmailVerified, false));
            await db.Users.Where(u => u.Email == oldVerified)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAtUtc, now.AddDays(-31)).SetProperty(u => u.IsEmailVerified, true));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetServices<IRecurringJob>().OfType<DataRetentionJob>().Single();
            Assert.True(await job.RunAsync(CancellationToken.None) >= 6);
        }

        await using var check = _fixture.CreateContext();
        var outbox = await check.OutboxMessages.Where(m => m.IdempotencyKey.EndsWith(tag)).Select(m => m.IdempotencyKey).ToListAsync();
        Assert.Equal(new[] { $"failed-20:{tag}", $"new-done:{tag}", $"old-pending:{tag}" }, outbox.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        // TargetHash là cột char(64) nên đọc ra có khoảng trắng đệm
        Assert.Equal([$"new-{tag}"], (await check.OtpChallenges.Where(c => c.TargetHash.Contains(tag)).Select(c => c.TargetHash).ToListAsync()).Select(h => h.TrimEnd()), StringComparer.Ordinal);
        Assert.Equal([$"new-{tag}"], await check.EmailLogs.Where(l => l.IdempotencyKey.EndsWith(tag)).Select(l => l.IdempotencyKey).ToListAsync());
        Assert.Equal([$"new-{tag}@example.com"], await check.ContactMessages.Where(m => m.Email.EndsWith(tag + "@example.com")).Select(m => m.Email).ToListAsync());
        Assert.False(await check.Users.AnyAsync(u => u.Email == oldUnverified));
        Assert.True(await check.Users.AnyAsync(u => u.Email == oldWithOrder));
        Assert.True(await check.Users.AnyAsync(u => u.Email == recentUnverified));
        Assert.True(await check.Users.AnyAsync(u => u.Email == oldVerified));
    }

    [Fact]
    public async Task Ngay_chay_viec_hang_ngay_luu_ben_trong_CSDL()
    {
        var name = $"test.daily.{Guid.NewGuid():N}";
        await using var scope = _factory.Services.CreateAsyncScope();
        var runs = scope.ServiceProvider.GetRequiredService<IJobRunStore>();

        Assert.Null(await runs.LastRunDateAsync(name, CancellationToken.None));
        await runs.RecordAsync(name, new DateOnly(2026, 11, 7), DateTime.UtcNow, CancellationToken.None);
        await runs.RecordAsync(name, new DateOnly(2026, 11, 8), DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 8), await runs.LastRunDateAsync(name, CancellationToken.None));
    }

    [Fact]
    public async Task Health_ready_chi_mo_cho_mang_noi_bo()
    {
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/health/live")).StatusCode);

        using var outside = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        outside.Headers.Add(TestRemoteIpStartupFilter.Header, "203.0.113.9");
        Assert.Equal(HttpStatusCode.NotFound, (await _http.SendAsync(outside)).StatusCode);

        using var lan = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        lan.Headers.Add(TestRemoteIpStartupFilter.Header, "192.168.1.20");
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(lan)).StatusCode);
    }
}
