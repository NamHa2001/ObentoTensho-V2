using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Notifications.Services;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.Core.Settings.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Maintenance;

// BR-LOY-04 (đối soát điểm hằng đêm), BR-NTF-12 (báo khi dùng ≥ 80% hạn mức email)
[Collection(SqlServerFixture.CollectionName)]
public sealed class ReconcileAndQuotaTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public ReconcileAndQuotaTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private async Task<Guid> MemberAsync()
    {
        var email = AuthApiClient.NewEmail();
        await new AuthApiClient(_http).RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    private async Task<int> RunAsync<T>()
        where T : IRecurringJob
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetServices<IRecurringJob>().OfType<T>().Single().RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Doi_soat_phat_hien_so_du_lech_so_cai_va_khong_tu_sua()
    {
        await _admin.SignInAsync(StaffRole.SuperAdmin);
        var consistent = await MemberAsync();
        var broken = await MemberAsync();
        var ledgerOnly = await MemberAsync();
        var lotsOnly = await MemberAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<LoyaltyLedger>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            foreach (var id in new[] { consistent, broken, ledgerOnly, lotsOnly })
            {
                await ledger.EarnAsync(id, LoyaltyTransactionType.AdminAdjust, $"test-{id:N}", 100, note: null, CancellationToken.None);
            }

            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Id == broken).ExecuteUpdateAsync(s => s.SetProperty(u => u.CurrentPoints, 150));

            // Chỉ sổ cái lệch: thêm một dòng sổ cái không có lô/số dư tương ứng
            db.LoyaltyPoints.Add(new ObentoTensho.Core.Loyalty.Domain.LoyaltyEntry
            {
                UserId = ledgerOnly, Points = 10, BalanceAfter = 110, Type = LoyaltyTransactionType.AdminAdjust, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            // Chỉ lô lệch
            await db.LoyaltyLots.Where(l => l.UserId == lotsOnly).ExecuteUpdateAsync(s => s.SetProperty(l => l.RemainingPoints, 90));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var mismatched = await scope.ServiceProvider.GetRequiredService<ILoyaltyRepository>().FindBalanceMismatchesAsync(100_000, CancellationToken.None);
            Assert.Contains(broken, mismatched);
            Assert.Contains(ledgerOnly, mismatched);
            Assert.Contains(lotsOnly, mismatched);
            Assert.DoesNotContain(consistent, mismatched);
        }

        Assert.True(await RunAsync<LoyaltyReconcileJob>() >= 1);
        await using var check = _fixture.CreateContext();
        Assert.Equal(150, await check.Users.Where(u => u.Id == broken).Select(u => u.CurrentPoints).SingleAsync());
        Assert.True(await check.OutboxMessages.AnyAsync(m => m.IdempotencyKey.StartsWith("admin_alert:loyalty-reconcile:")));
    }

    [Fact]
    public async Task Dung_qua_80_phan_tram_han_muc_thi_bao_mot_lan_trong_ngay()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        await using (var db = _fixture.CreateContext())
        {
            var now = DateTime.UtcNow;
            db.EmailLogs.AddRange(Enumerable.Range(0, 10).Select(i => new ObentoTensho.Core.Notifications.Domain.EmailLog
            {
                Type = "test", ToEmail = $"quota{i}@example.com", IdempotencyKey = $"quota:{Guid.NewGuid():N}", CreatedAtUtc = now, SentAtUtc = now, Status = EmailStatus.Sent,
            }));
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await RunAsync<EmailQuotaAlertJob>());
        await SetQuotaAsync("10");
        try
        {
            Assert.True(await RunAsync<EmailQuotaAlertJob>() >= 1);
            await RunAsync<EmailQuotaAlertJob>();

            await using var db = _fixture.CreateContext();
            var date = TestSchedule.TodayVn.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            var managerEmail = await db.StaffMembers.Where(s => s.Role == StaffRole.Manager).OrderByDescending(s => s.CreatedAtUtc).Select(s => s.Email).FirstAsync();
            Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.IdempotencyKey == $"admin_alert:email-quota:{date}:{managerEmail}"));
            Assert.NotNull(manager);
        }
        finally
        {
            await SetQuotaAsync("100000");
        }
    }

    private async Task SetQuotaAsync(string value)
    {
        await using (var db = _fixture.CreateContext())
        {
            await db.SystemSettings.Where(s => s.Key == SettingKeys.EmailDailyQuota).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, value));
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SettingsSeeder>().RefreshAsync(CancellationToken.None);
    }
}
