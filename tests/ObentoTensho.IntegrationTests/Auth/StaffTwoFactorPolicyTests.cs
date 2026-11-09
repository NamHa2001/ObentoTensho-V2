using System.Text.Json;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

[Collection(SqlServerFixture.CollectionName)]
public sealed class StaffTwoFactorPolicyTests(SqlServerFixture fixture)
{
    // Công tắc thử giao diện không có tác dụng ngoài môi trường Development
    [Fact]
    public async Task Cong_tac_bo_2FA_khong_co_tac_dung_ngoai_Development()
    {
        using var factory = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal) { ["Security:SkipStaffTwoFactor"] = "true" });
        var admin = new AdminAuthApiClient(factory, fixture);
        var (_, email) = await admin.CreateStaffAsync(StaffRole.SuperAdmin);

        var data = await AdminAuthApiClient.ReadDataAsync(await admin.LoginAsync(email));

        Assert.Equal(JsonValueKind.Null, data.GetProperty("session").ValueKind);
        Assert.Equal("totp_setup", data.GetProperty("challenge").GetProperty("kind").GetString());
    }
}
