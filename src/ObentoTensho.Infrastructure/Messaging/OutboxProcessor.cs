using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Notifications.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Infrastructure.Messaging;

// Xử lý một lô tin trong hộp thư đi. Mỗi tin một scope (DbContext riêng) để lỗi của tin này không ảnh hưởng tin khác.
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    IClock clock,
    ILogger<OutboxProcessor> logger)
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    // Lùi dần sau mỗi lần lỗi: 1′, 5′, 15′, 60′
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OutboxMessage> batch;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var now = clock.UtcNow;
            batch = await scope.ServiceProvider.GetRequiredService<OutboxStore>()
                .ClaimAsync(options.Value.BatchSize, now, now + Lease, cancellationToken);
        }

        foreach (var message in batch)
        {
            await ProcessAsync(message, cancellationToken);
        }

        return batch.Count;
    }

    private async Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<OutboxStore>();

        if (!string.Equals(message.Type, AccountEmailOutbox.EmailType, StringComparison.Ordinal)
            || TryReadEmail(message.PayloadJson) is not { } request)
        {
            await store.FailAsync(message.Id, clock.UtcNow, message.Attempts, "unsupported_message", cancellationToken);
            LogUnsupported(logger, message.Id, message.Type);
            return;
        }

        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatchService>();
        var outcome = await dispatcher.DispatchAsync(request, cancellationToken);
        switch (outcome)
        {
            case DispatchOutcome.Sent or DispatchOutcome.Suppressed:
                await store.CompleteAsync(message.Id, clock.UtcNow, cancellationToken);
                break;
            case DispatchOutcome.Deferred:
                await store.RescheduleAsync(message.Id, dispatcher.NextQuotaResetUtc(), message.Attempts, "daily_quota", cancellationToken);
                break;
            default:
                await RetryOrFailAsync(store, dispatcher, message, request, cancellationToken);
                break;
        }
    }

    private async Task RetryOrFailAsync(OutboxStore store, EmailDispatchService dispatcher, OutboxMessage message, EmailRequest request, CancellationToken cancellationToken)
    {
        var attempts = message.Attempts + 1;
        if (attempts >= options.Value.MaxAttempts)
        {
            await store.FailAsync(message.Id, clock.UtcNow, attempts, "send_failed", cancellationToken);
            await dispatcher.MarkFailedAsync(request.IdempotencyKey, cancellationToken);
            LogFailed(logger, message.Id, attempts);
            return;
        }

        var delay = Backoff[Math.Min(attempts - 1, Backoff.Length - 1)];
        await store.RescheduleAsync(message.Id, clock.UtcNow + delay, attempts, "send_failed", cancellationToken);
    }

    private static EmailRequest? TryReadEmail(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<EmailRequest>(payload, OutboxJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Tin hộp thư đi {Id} loại {Type} không xử lý được, chuyển sang thất bại.")]
    private static partial void LogUnsupported(ILogger logger, long id, string type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tin hộp thư đi {Id} thất bại sau {Attempts} lần thử.")]
    private static partial void LogFailed(ILogger logger, long id, int attempts);
}
