using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ObentoTensho.IntegrationTests.Fixtures;

// PERF-51, TST-28: đếm câu lệnh SQL của đúng một yêu cầu. Bộ đếm theo luồng async (AsyncLocal) nên việc nền
// chạy song song không bị tính; cần TestServer.PreserveExecutionContext = true để luồng của test đi vào server
public sealed class QueryCounter : DbCommandInterceptor
{
    public static readonly QueryCounter Instance = new();

    private static readonly AsyncLocal<Scope?> Current = new();

    private QueryCounter()
    {
    }

    public static Scope Begin()
    {
        var scope = new Scope();
        Current.Value = scope;
        return scope;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Count(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void Count(DbCommand command)
    {
        if (Current.Value is { } scope)
        {
            scope.Add(command.CommandText);
        }
    }

    public sealed class Scope : IDisposable
    {
        private readonly List<string> _commands = [];

        public int Count
        {
            get
            {
                lock (_commands)
                {
                    return _commands.Count;
                }
            }
        }

        // Để báo lỗi dễ đọc khi vượt giới hạn
        public string Describe()
        {
            lock (_commands)
            {
                return string.Join(Environment.NewLine + "---" + Environment.NewLine, _commands);
            }
        }

        public void Dispose() => Current.Value = null;

        internal void Add(string sql)
        {
            lock (_commands)
            {
                _commands.Add(sql);
            }
        }
    }
}
