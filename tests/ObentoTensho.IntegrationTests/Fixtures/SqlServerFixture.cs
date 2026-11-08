using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Tạo lại CSDL test sạch rồi chạy toàn bộ migration (docs/srs/07 TST-32).
// Máy dev: dùng SQL Server cục bộ (Windows Authentication). CI: đặt biến OBENTO_TEST_SQL.
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string CollectionName = "sql-server";

    private const string DatabaseName = "ObentoTensho_V2_Test";
    private const string ConnectionVariable = "OBENTO_TEST_SQL";
    private const string LocalDefault = "Server=localhost;Integrated Security=True;TrustServerCertificate=True";

    public string ConnectionString { get; private set; } = string.Empty;

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(ConnectionVariable);
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? LocalDefault : server)
        {
            InitialCatalog = "master",
        };

        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync();
            await ExecuteAsync(master, $"""
                IF DB_ID('{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END
                CREATE DATABASE [{DatabaseName}];
                """);
        }

        builder.InitialCatalog = DatabaseName;
        ConnectionString = builder.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        // Các test dùng chung EmailLogs trong ngày: hạn mức thật sẽ hoãn thư của test chạy sau
        context.SystemSettings.Add(new SystemSetting
        {
            Key = SettingKeys.EmailDailyQuota,
            Value = "100000",
            ValueType = SettingType.Number,
            Description = "Hạn mức test",
        });

        // Bật đủ ba ngôn ngữ để test đọc được bản dịch; test điều kiện bật ngôn ngữ tự hạ rồi khôi phục
        context.SystemSettings.Add(new SystemSetting
        {
            Key = SettingKeys.I18nEnabledLocales,
            Value = EnabledLocales,
            ValueType = SettingType.List,
            Description = "Ngôn ngữ test",
        });
        await context.SaveChangesAsync();
    }

    public const string EnabledLocales = "vi,en,ja";

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<object?> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
