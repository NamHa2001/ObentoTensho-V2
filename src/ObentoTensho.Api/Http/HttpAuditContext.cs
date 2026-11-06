using System.Diagnostics;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Http;

// Người gọi lấy từ access token đã xác thực; IP đã qua ForwardedHeaders (SEC-02)
public sealed class HttpAuditContext(IHttpContextAccessor accessor) : IAuditContext
{
    public AuditActor Actor
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity is not { IsAuthenticated: true } identity
                || !Guid.TryParse(user.FindFirst(AccessTokenClaims.Subject)?.Value, out var id))
            {
                return AuditActor.System;
            }

            return string.Equals(identity.AuthenticationType, AuthSchemes.Staff, StringComparison.Ordinal)
                ? AuditActor.Staff(id)
                : AuditActor.User(id);
        }
    }

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public Guid CorrelationId => CorrelationIds.FromActivity(Activity.Current) ?? CorrelationIds.FromTraceIdentifier(accessor.HttpContext?.TraceIdentifier);
}
