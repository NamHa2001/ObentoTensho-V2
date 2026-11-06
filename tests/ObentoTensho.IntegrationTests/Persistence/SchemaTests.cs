using Microsoft.EntityFrameworkCore;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Persistence;

// docs/srs/05 DB-01, DB-09, DB-12; docs/srs/07 TST-32
[Collection(SqlServerFixture.CollectionName)]
public sealed class SchemaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task TST_32_Migration_chay_tren_CSDL_rong_va_khop_voi_model()
    {
        await using var context = fixture.CreateContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task DB_09_Bat_READ_COMMITTED_SNAPSHOT()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var enabled = await SqlServerFixture.ScalarAsync(
            connection, "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()");

        Assert.Equal(true, enabled);
    }

    [Fact]
    public async Task DB_01_Moi_cot_thoi_gian_Utc_la_datetime2_3()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var total = (int)(await SqlServerFixture.ScalarAsync(connection,
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE COLUMN_NAME LIKE '%Utc'"))!;
        var wrong = (int)(await SqlServerFixture.ScalarAsync(connection,
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE COLUMN_NAME LIKE '%Utc' " +
            "AND (DATA_TYPE <> 'datetime2' OR DATETIME_PRECISION <> 3)"))!;

        Assert.True(total > 0);
        Assert.Equal(0, wrong);
    }

    [Theory]
    [InlineData("Users")]
    [InlineData("Staff")]
    [InlineData("RefreshTokens")]
    [InlineData("AuditLogs")]
    public async Task Bang_theo_08_ton_tai(string table)
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var count = (int)(await SqlServerFixture.ScalarAsync(connection,
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{table}'"))!;

        Assert.Equal(1, count);
    }
}
