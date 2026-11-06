using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Persistence;

// docs/srs/05 DB-02, DB-03; docs/srs/08 §1
[Collection(SqlServerFixture.CollectionName)]
public sealed class ConstraintTests(SqlServerFixture fixture)
{
    private const int CheckViolation = 547;
    private const int UniqueViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    [Fact]
    public async Task Email_khach_viet_hoa_bi_tu_choi()
    {
        await using var context = fixture.CreateContext();
        context.Users.Add(TestData.NewUser("Viet.Hoa@Example.Test"));

        Assert.Equal(CheckViolation, await SqlErrorOf(context));
    }

    [Fact]
    public async Task Email_khach_trung_bi_tu_choi()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        await using var first = fixture.CreateContext();
        first.Users.Add(TestData.NewUser(email));
        await first.SaveChangesAsync();

        await using var second = fixture.CreateContext();
        second.Users.Add(TestData.NewUser(email));

        Assert.Contains(await SqlErrorOf(second), new[] { UniqueViolation, UniqueConstraintViolation });
    }

    [Fact]
    public async Task Diem_am_bi_tu_choi()
    {
        await using var context = fixture.CreateContext();
        var user = TestData.NewUser();
        user.CurrentPoints = -1;
        context.Users.Add(user);

        Assert.Equal(CheckViolation, await SqlErrorOf(context));
    }

    [Fact]
    public async Task DEF_26_Moi_vai_tro_trong_enum_duoc_CHECK_chap_nhan()
    {
        await using var context = fixture.CreateContext();
        foreach (var role in Enum.GetValues<StaffRole>())
        {
            context.StaffMembers.Add(TestData.NewStaff(role));
        }

        await context.SaveChangesAsync();
        Assert.True(await context.StaffMembers.CountAsync() >= Enum.GetValues<StaffRole>().Length);
    }

    [Fact]
    public async Task DEF_26_Vai_tro_ngoai_enum_bi_CHECK_tu_choi()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var exception = await Assert.ThrowsAsync<SqlException>(() => SqlServerFixture.ExecuteAsync(connection, $"""
            INSERT INTO Staff (Id, FullName, Email, Role, IsActive, MustChangePassword, SecurityStamp, TotpEnabled, FailedLoginCount, CreatedAtUtc)
            VALUES (NEWID(), N'X', '{Guid.NewGuid():N}@example.test', 'Hacker', 1, 1, 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 0, 0, SYSUTCDATETIME())
            """));

        Assert.Equal(CheckViolation, exception.Number);
    }

    [Fact]
    public async Task Refresh_token_khong_co_chu_bi_tu_choi()
    {
        await using var context = fixture.CreateContext();
        context.RefreshTokens.Add(NewToken(userId: null, staffId: null));

        Assert.Equal(CheckViolation, await SqlErrorOf(context));
    }

    [Fact]
    public async Task Refresh_token_co_hai_chu_bi_tu_choi()
    {
        await using var context = fixture.CreateContext();
        var user = TestData.NewUser();
        var staff = TestData.NewStaff();
        context.AddRange(user, staff);
        await context.SaveChangesAsync();

        context.RefreshTokens.Add(NewToken(user.Id, staff.Id));

        Assert.Equal(CheckViolation, await SqlErrorOf(context));
    }

    [Fact]
    public async Task Refresh_token_cua_mot_chu_luu_duoc_va_ma_bam_la_duy_nhat()
    {
        await using var context = fixture.CreateContext();
        var user = TestData.NewUser();
        context.Users.Add(user);
        var token = NewToken(user.Id, null);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        await using var other = fixture.CreateContext();
        other.RefreshTokens.Add(NewToken(user.Id, null, token.TokenHash));

        Assert.Contains(await SqlErrorOf(other), new[] { UniqueViolation, UniqueConstraintViolation });
    }

    [Fact]
    public async Task DB_03_Cap_nhat_dong_thoi_cung_mot_khach_bi_phat_hien()
    {
        await using var setup = fixture.CreateContext();
        var user = TestData.NewUser();
        setup.Users.Add(user);
        await setup.SaveChangesAsync();

        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var a = await first.Users.SingleAsync(u => u.Id == user.Id);
        var b = await second.Users.SingleAsync(u => u.Id == user.Id);

        a.FullName = "Người sửa trước";
        await first.SaveChangesAsync();

        b.FullName = "Người sửa sau";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static RefreshToken NewToken(Guid? userId, Guid? staffId, string? hash = null) => new()
    {
        TokenHash = hash ?? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        FamilyId = Guid.CreateVersion7(),
        UserId = userId,
        StaffId = staffId,
        ExpiresAtUtc = TestData.Now.AddDays(7),
        CreatedAtUtc = TestData.Now,
    };

    private static async Task<int> SqlErrorOf(DbContext context)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        return Assert.IsType<SqlException>(exception.InnerException).Number;
    }
}
