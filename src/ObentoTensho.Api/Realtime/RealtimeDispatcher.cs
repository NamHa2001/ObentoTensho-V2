using Microsoft.AspNetCore.SignalR;
using ObentoTensho.Contracts.Realtime;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Reservations.Services;
using ObentoTensho.Infrastructure.Realtime;

namespace ObentoTensho.Api.Realtime;

// Gửi sự kiện đã commit tới đúng hub/nhóm. Lỗi một sự kiện không dừng các sự kiện sau.
public sealed partial class RealtimeDispatcher(
    RealtimeFeed feed,
    IHubContext<StaffHub> staffHub,
    IHubContext<PublicHub> publicHub,
    IServiceScopeFactory scopes,
    RealtimeMetrics metrics,
    ILogger<RealtimeDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in feed.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendAsync(message, stoppingToken);
                metrics.Sent(message.Event, message.EnqueuedTimestamp);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                metrics.Failed(message.Event);
                LogFailed(logger, message.Event, ex);
            }
        }
    }

    private async Task SendAsync(RealtimeMessage message, CancellationToken cancellationToken)
    {
        var payload = message.Payload is AvailabilityChange change ? await AvailabilityAsync(change, cancellationToken) : message.Payload;
        var staff = message.Groups.Where(g => g.StartsWith(RealtimeGroups.StaffPrefix, StringComparison.Ordinal)).ToArray();
        var visitors = message.Groups.Except(staff, StringComparer.Ordinal).ToArray();
        if (staff.Length > 0)
        {
            await staffHub.Clients.Groups(staff).SendAsync(message.Event, payload, cancellationToken);
        }

        if (visitors.Length > 0)
        {
            await publicHub.Clients.Groups(visitors).SendAsync(message.Event, payload, cancellationToken);
        }
    }

    // RT-01: số còn lại đọc sau commit, cùng cách tính với GET /reservations/availability; ca không còn nhận thì 0
    private async Task<SlotAvailabilityEventDto> AvailabilityAsync(AvailabilityChange change, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var day = await scope.ServiceProvider.GetRequiredService<ReservationSchedule>().GetAvailabilityAsync(change.Date, cancellationToken);
        var slot = day.Slots.FirstOrDefault(s => s.SlotId == change.SlotId);
        return new SlotAvailabilityEventDto(change.Date, change.SlotId, slot?.RemainingReservations ?? 0, slot is null ? 0 : slot.RemainingGuests);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Gửi sự kiện realtime {Event} thất bại.")]
    private static partial void LogFailed(ILogger logger, string @event, Exception exception);
}
