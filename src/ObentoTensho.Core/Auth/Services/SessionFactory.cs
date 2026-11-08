using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// Cấp cặp access + refresh token. Mỗi lần đăng nhập là một "gia đình" token (FamilyId);
// mỗi lần làm mới sinh token kế tiếp trong cùng gia đình (SEC-05).
public sealed class SessionFactory(
    IRefreshTokenRepository refreshTokens,
    ISecureTokenService secureTokens,
    IAccessTokenIssuer accessTokens,
    IClock clock)
{
    public const int UserAgentMaxLength = 300;

    public SecureToken NewRefreshToken() => secureTokens.Create();

    public string HashRefreshToken(string token) => secureTokens.Hash(token);

    // SEC-52: giữ tối đa N phiên; phiên cũ nhất bị thu hồi khi đăng nhập thêm
    public async Task EnforceUserSessionLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await refreshTokens.GetActiveUserFamiliesAsync(userId, clock.UtcNow, cancellationToken);
        await RevokeExcessAsync(active, cancellationToken);
    }

    public async Task EnforceStaffSessionLimitAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var active = await refreshTokens.GetActiveStaffFamiliesAsync(staffId, clock.UtcNow, cancellationToken);
        await RevokeExcessAsync(active, cancellationToken);
    }

    // Chỉ thêm token vào unit of work; bên gọi lưu thay đổi
    public AuthSession CreateForUser(User user, Guid familyId, SecureToken refreshToken, ClientInfo client)
    {
        var expiresAt = clock.UtcNow + AuthPolicy.RefreshTokenLifetime;
        AddRefreshToken(refreshToken, familyId, user.Id, staffId: null, expiresAt, client);
        return new AuthSession(accessTokens.IssueForCustomer(user.Id, user.SecurityStamp, familyId), refreshToken.Value, expiresAt);
    }

    // SEC-74: phiên nhân viên trượt theo hoạt động (30 phút) nhưng không vượt 12 giờ kể từ lúc đăng nhập
    public AuthSession CreateForStaff(Staff staff, Guid familyId, DateTime familyStartedAtUtc, SecureToken refreshToken, ClientInfo client)
    {
        var idleExpiry = clock.UtcNow + AuthPolicy.StaffIdleTimeout;
        var absoluteExpiry = familyStartedAtUtc + AuthPolicy.StaffAbsoluteLifetime;
        var expiresAt = idleExpiry < absoluteExpiry ? idleExpiry : absoluteExpiry;

        AddRefreshToken(refreshToken, familyId, userId: null, staff.Id, expiresAt, client);
        return new AuthSession(accessTokens.IssueForStaff(staff.Id, staff.Role, staff.SecurityStamp, familyId), refreshToken.Value, expiresAt);
    }

    private async Task RevokeExcessAsync(IReadOnlyList<Guid> activeFamilies, CancellationToken cancellationToken)
    {
        var excess = activeFamilies.Skip(AuthPolicy.MaxSessionsPerAccount - 1).ToArray();
        if (excess.Length > 0)
        {
            await refreshTokens.RevokeFamiliesAsync(excess, RefreshTokenRevokedReason.SessionLimit, clock.UtcNow, cancellationToken);
        }
    }

    private void AddRefreshToken(SecureToken token, Guid familyId, Guid? userId, Guid? staffId, DateTime expiresAt, ClientInfo client) =>
        refreshTokens.Add(new RefreshToken
        {
            TokenHash = token.Hash,
            FamilyId = familyId,
            UserId = userId,
            StaffId = staffId,
            ExpiresAtUtc = expiresAt,
            CreatedByIp = client.IpAddress,
            UserAgent = client.UserAgent is { Length: > UserAgentMaxLength } ua ? ua[..UserAgentMaxLength] : client.UserAgent,
            CreatedAtUtc = clock.UtcNow,
        });
}
