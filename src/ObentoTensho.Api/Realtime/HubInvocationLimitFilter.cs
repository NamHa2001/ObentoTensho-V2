using ObentoTensho.Contracts.Errors;
using Microsoft.AspNetCore.SignalR;

namespace ObentoTensho.Api.Realtime;

// SEC-69: giới hạn số lệnh mỗi kết nối gửi lên (hub chỉ nhận lệnh nhẹ: theo dõi ngày/đơn)
public sealed class HubInvocationLimitFilter(TimeProvider time) : IHubFilter
{
    public const int MaxPerWindow = 10;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);
    private const string Key = "invocations";

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);
        var items = invocationContext.Context.Items;
        var now = time.GetUtcNow();
        var (start, count) = items.TryGetValue(Key, out var value) && value is (DateTimeOffset s, int c) && now - s < Window ? (s, c) : (now, 0);
        if (count >= MaxPerWindow)
        {
            throw new HubException(ErrorCodes.RateLimited);
        }

        items[Key] = (start, count + 1);
        return await next(invocationContext);
    }
}
