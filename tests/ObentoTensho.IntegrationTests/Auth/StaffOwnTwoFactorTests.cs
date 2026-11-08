using System.Net;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// docs/srs/10 §4 (2fa/recovery), SEC-61: nhân viên tự bật 2FA, tạo lại mã khôi phục, tắt 2FA (cần step-up)
[Collection(SqlServerFixture.CollectionName)]
public sealed class StaffOwnTwoFactorTests : IDisposable
{
    private const string Me = "/api/v1/admin/auth/me/2fa";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _admin;

    public StaffOwnTwoFactorTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<(Guid Id, string Email, string Token)> KitchenAsync()
    {
        var (id, email) = await _admin.CreateStaffAsync(StaffRole.Kitchen);
        var data = await AdminAuthApiClient.ReadDataAsync(await _admin.LoginAsync(email));
        return (id, email, data.GetProperty("session").GetProperty("accessToken").GetString()!);
    }

    [Fact]
    public async Task Bat_2FA_tu_nguyen_tao_lai_ma_khoi_phuc_va_tat()
    {
        var (id, email, token) = await KitchenAsync();

        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Post, $"{Me}/setup", token)).StatusCode);
        var secret = await _admin.ReadTotpSecretAsync(id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.SendAsync(HttpMethod.Post, $"{Me}/setup/confirm", token, new { code = "000000" })).StatusCode);
        var first = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Me}/setup/confirm", token, new { code = AdminAuthApiClient.CurrentCode(secret) }));
        var firstCodes = first.GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(10, firstCodes.Length);
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Post, $"{Me}/setup", token)).StatusCode);

        // Đăng nhập lại giờ phải qua bước 2FA
        var login = await AdminAuthApiClient.ReadDataAsync(await _admin.LoginAsync(email));
        Assert.Equal("totp_verify", login.GetProperty("challenge").GetProperty("kind").GetString());

        // Tạo lại mã khôi phục cần step-up; mã cũ hết hiệu lực
        var noStepUp = await _admin.SendAsync(HttpMethod.Post, $"{Me}/recovery-codes", token);
        Assert.Equal(ErrorCodes.StepUpRequired, await AuthApiClient.ReadErrorCodeAsync(noStepUp));
        var second = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Me}/recovery-codes", token, stepUp: await _admin.StepUpAsync(token)));
        Assert.Equal(10, second.GetProperty("recoveryCodes").GetArrayLength());
        var challenge = login.GetProperty("challenge").GetProperty("challengeToken").GetString();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.PostAsync("2fa/verify", new { challengeToken = challenge, code = (string?)null, recoveryCode = firstCodes[0] })).StatusCode);

        // Tắt 2FA (vai trò không bắt buộc) cần step-up
        Assert.Equal(ErrorCodes.StepUpRequired, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, $"{Me}/disable", token)));
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Post, $"{Me}/disable", token, stepUp: await _admin.StepUpAsync(token))).StatusCode);

        await using var db = _fixture.CreateContext();
        var member = await db.StaffMembers.SingleAsync(s => s.Id == id);
        Assert.False(member.TotpEnabled);
        Assert.Null(member.TotpSecretEncrypted);
        Assert.False(await db.StaffRecoveryCodes.AnyAsync(c => c.StaffId == id));
        Assert.Equal(3, await db.OutboxMessages.CountAsync(m => m.IdempotencyKey.StartsWith($"admin_alert:two-factor:{id:N}:")));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "auth.staff.two_factor_disabled"));
    }

    [Fact]
    public async Task Vai_tro_bat_buoc_2FA_khong_tat_duoc()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        var response = await _admin.SendAsync(HttpMethod.Post, $"{Me}/disable", manager, stepUp: await _admin.StepUpAsync(manager));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
