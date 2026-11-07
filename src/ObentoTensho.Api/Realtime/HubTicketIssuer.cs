using System.Globalization;
using System.Security.Claims;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Contracts.Realtime;
using ObentoTensho.Core.Common;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Realtime;

// Phát vé cho người gọi đã xác thực bằng access token (scheme khách hoặc nhân viên)
public sealed class HubTicketIssuer(HubTicketStore tickets)
{
    public Result<HubTicketDto> Issue(ClaimsPrincipal user, string scheme, string kind)
    {
        ArgumentNullException.ThrowIfNull(user);
        var identity = user.Identities.FirstOrDefault(i => i.IsAuthenticated && string.Equals(i.AuthenticationType, scheme, StringComparison.Ordinal));
        if (identity is null
            || !Guid.TryParse(identity.FindFirst(AccessTokenClaims.Subject)?.Value, out var id)
            || identity.FindFirst(AccessTokenClaims.SecurityStamp)?.Value is not { } stamp
            || !long.TryParse(identity.FindFirst("exp")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp))
        {
            return Result.Failure<HubTicketDto>(ErrorCodes.Unauthenticated);
        }

        var role = string.Equals(kind, HubIdentity.StaffKind, StringComparison.Ordinal) ? identity.FindFirst(AccessTokenClaims.Role)?.Value : null;
        var issued = tickets.Issue(new HubIdentity(kind, id, role, stamp, DateTimeOffset.FromUnixTimeSeconds(exp)));
        return issued is { } value
            ? Result.Success(new HubTicketDto(value.Ticket, value.ExpiresAt.UtcDateTime))
            : Result.Failure<HubTicketDto>(ErrorCodes.RateLimited);
    }
}
