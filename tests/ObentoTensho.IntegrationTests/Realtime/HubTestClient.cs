using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Realtime;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Realtime;

// Kết nối SignalR thật (WebSocket qua TestServer) như trình duyệt: gửi Origin, vé trên query; ghi lại mọi sự kiện nhận được
public sealed class HubTestClient : IAsyncDisposable
{
    private static readonly string[] Events =
    [
        RealtimeEventNames.ReservationCreated,
        RealtimeEventNames.ReservationStatusChanged,
        RealtimeEventNames.SlotAvailabilityChanged,
        RealtimeEventNames.OrderCreated,
        RealtimeEventNames.OrderStatusChanged,
        RealtimeEventNames.ContactCreated,
    ];

    private readonly Channel<(string Event, JsonElement Payload)> _received = Channel.CreateUnbounded<(string, JsonElement)>();
    private readonly List<(string Event, JsonElement Payload)> _seen = [];
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HubTestClient(HubConnection connection)
    {
        Connection = connection;
        foreach (var name in Events)
        {
            connection.On<JsonElement>(name, payload => _received.Writer.TryWrite((name, payload)));
        }

        connection.Closed += _ =>
        {
            _closed.TrySetResult();
            return Task.CompletedTask;
        };
    }

    public HubConnection Connection { get; }

    public static HubTestClient Create(ApiFactory factory, string route, string origin, string? ticket = null)
    {
        var server = factory.Server;
        var query = ticket is null ? string.Empty : "?ticket=" + Uri.EscapeDataString(ticket);
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost" + route + query), o =>
            {
                o.Transports = HttpTransportType.WebSockets;
                o.Headers["Origin"] = origin;
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                o.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var client = server.CreateWebSocketClient();
                    client.ConfigureRequest = request => request.Headers.Origin = origin;
                    return await client.ConnectAsync(context.Uri, cancellationToken);
                };
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)))
            .Build();
        return new HubTestClient(connection);
    }

    public static async Task<HubTestClient> ConnectAsync(ApiFactory factory, string route, string origin, string? ticket = null)
    {
        var client = Create(factory, route, origin, ticket);
        await client.Connection.StartAsync();
        return client;
    }

    // Chờ sự kiện khớp điều kiện (bỏ qua sự kiện của test khác cùng nhóm)
    public async Task<JsonElement> NextAsync(string name, Func<JsonElement, bool> match, int timeoutMs = 5000)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        if (_seen.FirstOrDefault(e => string.Equals(e.Event, name, StringComparison.Ordinal) && match(e.Payload)) is { Event: not null } early)
        {
            return early.Payload;
        }

        while (true)
        {
            var item = await _received.Reader.ReadAsync(timeout.Token);
            _seen.Add(item);
            if (string.Equals(item.Event, name, StringComparison.Ordinal) && match(item.Payload))
            {
                return item.Payload;
            }
        }
    }

    // Không nhận sự kiện khớp điều kiện trong khoảng chờ
    public async Task<bool> ReceivedWithinAsync(string name, Func<JsonElement, bool> match, int waitMs = 1500)
    {
        try
        {
            await NextAsync(name, match, waitMs);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    // Số sự kiện khớp điều kiện nhận được sau khoảng chờ
    public async Task<int> CountAsync(string name, Func<JsonElement, bool> match, int waitMs = 1500)
    {
        await Task.Delay(waitMs);
        while (_received.Reader.TryRead(out var item))
        {
            _seen.Add(item);
        }

        return _seen.Count(e => string.Equals(e.Event, name, StringComparison.Ordinal) && match(e.Payload));
    }

    public async Task<bool> ClosedWithinAsync(int waitMs = 5000) =>
        await Task.WhenAny(_closed.Task, Task.Delay(waitMs)) == _closed.Task;

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
