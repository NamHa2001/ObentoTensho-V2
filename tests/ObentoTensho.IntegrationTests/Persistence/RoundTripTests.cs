using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Persistence;

// Lưu rồi đọc lại: kiểu dữ liệu, UTC, enum dạng chuỗi (docs/srs/08)
[Collection(SqlServerFixture.CollectionName)]
public sealed class RoundTripTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Khach_luu_va_doc_lai_dung_gia_tri_gio_UTC_va_RowVersion()
    {
        var user = TestData.NewUser();
        user.DateOfBirth = new DateOnly(1999, 5, 20);
        user.MemberTier = MemberTier.Gold;
        user.LockoutEndUtc = TestData.Now.AddMinutes(15);

        await using (var write = fixture.CreateContext())
        {
            write.Users.Add(user);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var loaded = await read.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);

        Assert.Equal(new DateOnly(1999, 5, 20), loaded.DateOfBirth);
        Assert.Equal(MemberTier.Gold, loaded.MemberTier);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
        Assert.Equal(TestData.Now, loaded.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.LockoutEndUtc!.Value.Kind);
        Assert.NotEmpty(loaded.RowVersion);
        Assert.True(loaded.IsActive);
        Assert.Equal("vi", loaded.PreferredLang);
    }

    [Fact]
    public async Task Khoa_chinh_Guid_v7_tang_dan_theo_thoi_gian()
    {
        var first = TestData.NewUser();
        await Task.Delay(5);
        var second = TestData.NewUser();

        Assert.True(string.CompareOrdinal(first.Id.ToString(), second.Id.ToString()) < 0);
        Assert.Equal(7, first.Id.Version);
    }

    [Fact]
    public async Task Nhan_vien_luu_vai_tro_dang_chuoi_va_mat_khau_co_the_rong()
    {
        var staff = TestData.NewStaff(StaffRole.Kitchen);

        await using (var write = fixture.CreateContext())
        {
            write.StaffMembers.Add(staff);
            await write.SaveChangesAsync();
        }

        await using var connection = await fixture.OpenConnectionAsync();
        var role = await SqlServerFixture.ScalarAsync(connection, $"SELECT Role FROM Staff WHERE Id = '{staff.Id}'");
        Assert.Equal("Kitchen", role);

        await using var read = fixture.CreateContext();
        var loaded = await read.StaffMembers.AsNoTracking().SingleAsync(s => s.Id == staff.Id);
        Assert.Null(loaded.PasswordHash);
        Assert.True(loaded.MustChangePassword);
    }

    [Fact]
    public async Task Nhat_ky_luu_enum_dang_snake_case()
    {
        var log = new AuditLog
        {
            ActorType = AuditActorType.Staff,
            ActorId = Guid.CreateVersion7(),
            Action = "Đổi giờ mở cửa",
            EntityType = "OpeningHours",
            EntityId = "1",
            NewDataJson = "{}",
            CorrelationId = Guid.NewGuid(),
            CreatedAtUtc = TestData.Now,
        };

        await using (var write = fixture.CreateContext())
        {
            write.AuditLogs.Add(log);
            await write.SaveChangesAsync();
        }

        await using var connection = await fixture.OpenConnectionAsync();
        var actor = await SqlServerFixture.ScalarAsync(connection, $"SELECT ActorType FROM AuditLogs WHERE Id = {log.Id}");
        Assert.Equal("staff", actor);
    }

    [Fact]
    public async Task Refresh_token_luu_ly_do_thu_hoi_dang_snake_case()
    {
        await using var write = fixture.CreateContext();
        var user = TestData.NewUser();
        write.Users.Add(user);
        var token = new RefreshToken
        {
            TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FamilyId = Guid.CreateVersion7(),
            UserId = user.Id,
            ExpiresAtUtc = TestData.Now.AddDays(7),
            CreatedAtUtc = TestData.Now,
            RevokedAtUtc = TestData.Now,
            RevokedReason = RefreshTokenRevokedReason.ReuseDetected,
        };
        write.RefreshTokens.Add(token);
        await write.SaveChangesAsync();

        await using var connection = await fixture.OpenConnectionAsync();
        var reason = await SqlServerFixture.ScalarAsync(connection, $"SELECT RevokedReason FROM RefreshTokens WHERE Id = {token.Id}");
        Assert.Equal("reuse_detected", reason);

        await using var read = fixture.CreateContext();
        var loaded = await read.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id);
        Assert.Equal(RefreshTokenRevokedReason.ReuseDetected, loaded.RevokedReason);
    }
}
