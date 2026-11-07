using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Realtime.Domain;

namespace ObentoTensho.Api.Realtime;

// SEC-04: chỉ nhân viên có vé hợp lệ; nhóm do server gán theo vai trò, client không có lệnh nào để tự vào nhóm
[Authorize(Policy = RealtimeConfiguration.StaffHubPolicy)]
public sealed class StaffHub(HubConnectionTracker tracker) : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Kết quả xác thực của chính request này (đã đổi vé ở bước phân quyền, không đổi lần nữa)
        var http = Context.GetHttpContext()!;
        var result = await http.AuthenticateAsync(HubTicketHandler.SchemeName);
        var identity = HubTicketHandler.IdentityOf(result.Principal, result.Properties?.ExpiresUtc);
        if (identity?.Role is not { } role || !Enum.TryParse<StaffRole>(role, ignoreCase: false, out var staffRole)
            || !tracker.TryAdd(Context, RateLimitConfiguration.ClientKey(http), identity))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Staff(staffRole));
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        tracker.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
