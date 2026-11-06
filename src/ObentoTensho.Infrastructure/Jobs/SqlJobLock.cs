using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ObentoTensho.Infrastructure.Jobs;

// Khóa ứng dụng của SQL Server (sp_getapplock) theo tên việc: nhiều instance cùng chạy thì chỉ một instance làm,
// instance kia bỏ lượt (timeout 0). Khóa gắn với kết nối nên tự nhả khi tiến trình chết.
public sealed class SqlJobLock(IConfiguration configuration)
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(string name, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(configuration.GetConnectionString("Default"));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@Resource", "obento-job:" + name);
        command.Parameters.AddWithValue("@LockMode", "Exclusive");
        command.Parameters.AddWithValue("@LockOwner", "Session");
        command.Parameters.AddWithValue("@LockTimeout", 0);
        var result = command.Parameters.Add("@Result", System.Data.SqlDbType.Int);
        result.Direction = System.Data.ParameterDirection.ReturnValue;
        await command.ExecuteNonQueryAsync(cancellationToken);

        // 0: cấp ngay; 1: cấp sau khi chờ; số âm: không được cấp
        if ((int)result.Value < 0)
        {
            await connection.DisposeAsync();
            return null;
        }

        return connection;
    }
}
