using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Realtime;

// Đổi vé (?ticket=) lấy danh tính khi nối hub. Hết hạn theo access token gốc để SignalR tự đóng kết nối (SEC-69).
public sealed class HubTicketHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    HubTicketStore tickets) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "hub-ticket";
    public const string KindClaim = "kind";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ticket = Request.Query[HubRoutes.TicketQuery].ToString();
        if (ticket.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var negotiating = Request.Path.Value?.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase) == true;
        var identity = negotiating ? tickets.Peek(ticket) : tickets.Redeem(ticket);
        if (identity is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Vé không hợp lệ hoặc đã dùng."));
        }

        var claims = new List<Claim>
        {
            new(AccessTokenClaims.Subject, identity.Id.ToString("D")),
            new(AccessTokenClaims.SecurityStamp, identity.SecurityStamp),
            new(KindClaim, identity.Kind),
        };
        if (identity.Role is { } role)
        {
            claims.Add(new Claim(AccessTokenClaims.Role, role));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, AccessTokenClaims.Subject, AccessTokenClaims.Role));
        var properties = new AuthenticationProperties { ExpiresUtc = identity.ExpiresAt };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, SchemeName)));
    }

    public static HubIdentity? IdentityOf(ClaimsPrincipal? user, DateTimeOffset? expiresAt)
    {
        var identity = user?.Identities.FirstOrDefault(i => i.IsAuthenticated && string.Equals(i.AuthenticationType, SchemeName, StringComparison.Ordinal));
        return identity is not null
            && Guid.TryParse(identity.FindFirst(AccessTokenClaims.Subject)?.Value, out var id)
            && identity.FindFirst(KindClaim)?.Value is { } kind
            && identity.FindFirst(AccessTokenClaims.SecurityStamp)?.Value is { } stamp
            ? new HubIdentity(kind, id, identity.FindFirst(AccessTokenClaims.Role)?.Value, stamp, expiresAt ?? DateTimeOffset.MaxValue)
            : null;
    }
}
