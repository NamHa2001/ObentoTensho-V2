using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using ObentoTensho.Api.Http;
using ObentoTensho.Api.Realtime;

namespace ObentoTensho.Api.Configuration;

// docs/srs/06 Phần B, SEC-04, SEC-69
public static class RealtimeConfiguration
{
    public const string StaffHubPolicy = "hub:staff";
    private const int MaxMessageBytes = 8 * 1024;

    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        services.AddSingleton<HubTicketStore>();
        services.AddSingleton<HubTicketIssuer>();
        services.AddSingleton<HubConnectionTracker>();
        services.AddSingleton<RealtimeMetrics>();
        services.AddSingleton<HubInvocationLimitFilter>();
        services.AddSingleton<HubSessionSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<HubSessionSweeper>());
        services.AddHostedService<RealtimeDispatcher>();

        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HubTicketHandler>(HubTicketHandler.SchemeName, configureOptions: null);
        services.AddAuthorizationBuilder().AddPolicy(StaffHubPolicy, policy => policy
            .AddAuthenticationSchemes(HubTicketHandler.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(HubTicketHandler.KindClaim, HubIdentity.StaffKind));

        services.AddSignalR(o =>
        {
            o.MaximumReceiveMessageSize = MaxMessageBytes;
            o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            o.MaximumParallelInvocationsPerClient = 1;
            o.EnableDetailedErrors = false;
            o.AddFilter<HubInvocationLimitFilter>();
        }).AddJsonProtocol(o =>
        {
            o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
            o.PayloadSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
        });

        return services;
    }

    // Hub nhân viên chỉ WebSocket: vé dùng một lần nên các transport cần nhiều request (long polling, SSE) không giữ được danh tính
    public static WebApplication MapRealtimeHubs(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHub<StaffHub>(HubRoutes.Staff, o =>
        {
            o.Transports = HttpTransportType.WebSockets;
            o.CloseOnAuthenticationExpiration = true;
        }).RequireCors(AuthConfiguration.AdminCorsPolicy);
        app.MapHub<PublicHub>(HubRoutes.Public, o => o.CloseOnAuthenticationExpiration = true);
        return app;
    }
}
