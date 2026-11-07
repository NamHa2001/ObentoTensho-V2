using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Core.Orders.Services;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Api.Realtime;

// docs/srs/06 §B.2: ẩn danh, chỉ cho nhóm số chỗ theo ngày (không nhạy cảm) và nhóm đơn khi chứng minh được quyền xem đơn
[AllowAnonymous]
public sealed class PublicHub(
    HubConnectionTracker tracker,
    PublicOrderService orders,
    IReservationSettings reservationSettings,
    IClock clock) : Hub
{
    public const int MaxWatchedOrders = 5;
    private const string AvailabilityKey = "availability";
    private const string OrdersKey = "orders";
    private const string IdentityKey = "identity";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        var result = await http.AuthenticateAsync(HubTicketHandler.SchemeName);
        var identity = result.Succeeded ? HubTicketHandler.IdentityOf(result.Principal, result.Properties?.ExpiresUtc) : null;

        // Có vé mà vé sai/đã dùng thì từ chối, không lặng lẽ thành ẩn danh
        if ((result.Failure is not null) || (identity is not null && identity.IsStaff)
            || !tracker.TryAdd(Context, RateLimitConfiguration.ClientKey(http), identity))
        {
            Context.Abort();
            return;
        }

        Context.Items[IdentityKey] = identity;
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        tracker.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    // RT-01: mỗi kết nối xem một ngày; chọn ngày khác thì rời nhóm cũ
    public async Task<bool> WatchAvailability(DateOnly date)
    {
        var today = clock.TodayLocal;
        if (date < today || date > today.AddDays(reservationSettings.MaxAdvanceDays))
        {
            return false;
        }

        if (Context.Items.TryGetValue(AvailabilityKey, out var previous) && previous is string old)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, old);
        }

        var group = RealtimeGroups.Availability(date);
        Context.Items[AvailabilityKey] = group;
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        return true;
    }

    // RT-03: khách không tài khoản theo dõi đơn bằng mã + token trong email (cùng giới hạn sai như REST)
    public async Task<bool> WatchOrder(string code, string token)
    {
        var result = await orders.FindTrackedIdAsync(new TrackOrderRequest(code, token), Context.ConnectionAborted);
        return result.IsSuccess && await JoinOrderAsync(result.Value);
    }

    // Khách đã đăng nhập (nối bằng vé) theo dõi đơn của chính mình
    public async Task<bool> WatchMyOrder(string code)
    {
        if (!Context.Items.TryGetValue(IdentityKey, out var value) || value is not HubIdentity { IsStaff: false } identity)
        {
            return false;
        }

        var result = await orders.FindMineIdAsync(identity.Id, code, Context.ConnectionAborted);
        return result.IsSuccess && await JoinOrderAsync(result.Value);
    }

    private async Task<bool> JoinOrderAsync(Guid orderId)
    {
        var watched = Context.Items.TryGetValue(OrdersKey, out var value) && value is HashSet<Guid> set ? set : [];
        if (!watched.Contains(orderId) && watched.Count >= MaxWatchedOrders)
        {
            return false;
        }

        watched.Add(orderId);
        Context.Items[OrdersKey] = watched;
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Order(orderId));
        return true;
    }
}
