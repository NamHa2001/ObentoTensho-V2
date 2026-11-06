using Microsoft.Data.SqlClient;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Persistence;

// docs/srs/05 DB-10, docs/srs/07 TST-31: tài khoản chạy ứng dụng không có DDL và không sửa/xóa được nhật ký.
[Collection(SqlServerFixture.CollectionName)]
public sealed class DatabasePermissionTests(SqlServerFixture fixture)
{
    private const string TestUser = "ot_test_runtime";
    private const int PermissionDenied = 229;
    private const int CreateObjectDenied = 262;
    // SQL Server báo "không tồn tại hoặc không có quyền" khi thiếu quyền ALTER/DROP trên đối tượng
    private const int AlterDeniedOrHidden = 1088;
    private const int DropDeniedOrHidden = 3701;

    private const string InsertAudit = """
        INSERT INTO AuditLogs (ActorType, Action, EntityType, EntityId, CorrelationId, CreatedAtUtc)
        VALUES ('system', N'kiểm tra quyền', 'Test', '1', NEWID(), SYSUTCDATETIME())
        """;

    [Fact]
    public async Task TST_31_Chay_ung_dung_duoc_them_va_doc_nhat_ky()
    {
        await using var connection = await OpenAsRuntimeUserAsync();
        try
        {
            await SqlServerFixture.ExecuteAsync(connection, InsertAudit);
            var count = (int)(await SqlServerFixture.ScalarAsync(connection, "SELECT COUNT(*) FROM AuditLogs"))!;
            Assert.True(count > 0);
        }
        finally
        {
            await SqlServerFixture.ExecuteAsync(connection, "REVERT;");
        }
    }

    [Theory]
    [InlineData("UPDATE AuditLogs SET Action = N'sửa trộm'")]
    [InlineData("DELETE FROM AuditLogs")]
    public async Task TST_31_Chay_ung_dung_khong_sua_hay_xoa_duoc_nhat_ky(string sql)
    {
        await using var connection = await OpenAsRuntimeUserAsync();
        try
        {
            await SqlServerFixture.ExecuteAsync(connection, InsertAudit);

            var exception = await Assert.ThrowsAsync<SqlException>(() => SqlServerFixture.ExecuteAsync(connection, sql));
            Assert.Equal(PermissionDenied, exception.Number);
        }
        finally
        {
            await SqlServerFixture.ExecuteAsync(connection, "REVERT;");
        }
    }

    [Theory]
    [InlineData("CREATE TABLE dbo.KhongDuocTao (Id int)", CreateObjectDenied)]
    [InlineData("ALTER TABLE dbo.Users ADD CotLa int NULL", AlterDeniedOrHidden)]
    [InlineData("DROP TABLE dbo.AuditLogs", DropDeniedOrHidden)]
    public async Task TST_31_Chay_ung_dung_khong_co_quyen_DDL(string sql, int expectedError)
    {
        await using var connection = await OpenAsRuntimeUserAsync();
        try
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() => SqlServerFixture.ExecuteAsync(connection, sql));
            Assert.Equal(expectedError, exception.Number);
        }
        finally
        {
            await SqlServerFixture.ExecuteAsync(connection, "REVERT;");
        }
    }

    // Người dùng thử không có đăng nhập, thuộc vai trò ot_app_runtime do migration tạo
    private async Task<SqlConnection> OpenAsRuntimeUserAsync()
    {
        var connection = await fixture.OpenConnectionAsync();
        await SqlServerFixture.ExecuteAsync(connection, $"""
            IF DATABASE_PRINCIPAL_ID('{TestUser}') IS NULL
            BEGIN
                CREATE USER [{TestUser}] WITHOUT LOGIN;
                ALTER ROLE [ot_app_runtime] ADD MEMBER [{TestUser}];
            END
            """);
        await SqlServerFixture.ExecuteAsync(connection, $"EXECUTE AS USER = '{TestUser}';");
        return connection;
    }
}
