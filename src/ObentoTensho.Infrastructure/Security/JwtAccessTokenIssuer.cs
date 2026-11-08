using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Infrastructure.Security;

// docs/srs/09 §3: claim sub, role, sv, sid, jti, iat, exp, iss, aud; header có kid; không chứa email/SĐT.
// Audience khác nhau cho khách và nhân viên nên token khách không dùng được ở API quản trị.
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, IClock clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken IssueForCustomer(Guid userId, string securityStamp, Guid sessionId) =>
        Issue(options.Value.CustomerAudience, userId, AuthPolicy.CustomerRole, securityStamp, sessionId);

    public AccessToken IssueForStaff(Guid staffId, StaffRole role, string securityStamp, Guid sessionId) =>
        Issue(options.Value.AdminAudience, staffId, role.ToString(), securityStamp, sessionId);

    private AccessToken Issue(string audience, Guid subject, string role, string securityStamp, Guid sessionId)
    {
        var jwt = options.Value;
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(jwt.GetActiveKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [AccessTokenClaims.Subject] = subject.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString("N"),
                [AccessTokenClaims.Role] = role,
                [AccessTokenClaims.SecurityStamp] = securityStamp,
                [AccessTokenClaims.SessionId] = sessionId.ToString("N"),
            },
        });

        return new AccessToken(token, expiresAt);
    }
}
