using System.Security.Claims;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Http;

// Định danh người gọi lấy từ access token đã xác thực — không bao giờ nhận từ body (SEC-13, SEC-49)
public static class ClaimsPrincipalExtensions
{
    public static Guid? CustomerId(this ClaimsPrincipal user) => SubjectFor(user, AuthSchemes.Customer);

    public static Guid StaffId(this ClaimsPrincipal user) =>
        SubjectFor(user, AuthSchemes.Staff) ?? throw new InvalidOperationException("Endpoint quản trị gọi khi chưa xác thực nhân viên.");

    public static StaffRole StaffRole(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Enum.TryParse<StaffRole>(user.FindFirst(AccessTokenClaims.Role)?.Value, ignoreCase: false, out var role)
            ? role
            : throw new InvalidOperationException("Access token nhân viên thiếu vai trò.");
    }

    private static Guid? SubjectFor(ClaimsPrincipal user, string scheme)
    {
        ArgumentNullException.ThrowIfNull(user);
        var identity = user.Identities.FirstOrDefault(i => i.IsAuthenticated && string.Equals(i.AuthenticationType, scheme, StringComparison.Ordinal));
        return Guid.TryParse(identity?.FindFirst(AccessTokenClaims.Subject)?.Value, out var id) ? id : null;
    }
}
