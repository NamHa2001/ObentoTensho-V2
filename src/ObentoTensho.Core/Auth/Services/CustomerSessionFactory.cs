using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// Cấp cặp access + refresh token cho khách. Mỗi lần đăng nhập là một "gia đình" token (FamilyId);
// mỗi lần làm mới sinh token kế tiếp trong cùng gia đình (SEC-05).
public sealed class CustomerSessionFactory(
    IRefreshTokenRepository refreshTokens,
    ISecureTokenService secureTokens,
    IAccessTokenIssuer accessTokens,
    IClock clock)
{
    private const int UserAgentMaxLength = 300;

    public SecureToken NewRefreshToken() => secureTokens.Create();

    public string HashRefreshToken(string token) => secureTokens.Hash(token);

    // SEC-52: giữ tối đa N phiên; phiên cũ nhất bị thu hồi khi đăng nhập thêm
    public async Task EnforceSessionLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var active = await refreshTokens.GetActiveUserFamiliesAsync(userId, now, cancellationToken);
        var excess = active.Skip(AuthPolicy.MaxSessionsPerAccount - 1).ToArray();
        if (excess.Length > 0)
        {
            await refreshTokens.RevokeFamiliesAsync(excess, RefreshTokenRevokedReason.SessionLimit, now, cancellationToken);
        }
    }

    // Chỉ thêm token vào unit of work; bên gọi lưu thay đổi
    public AuthSession Create(User user, Guid familyId, SecureToken refreshToken, ClientInfo client)
    {
        var now = clock.UtcNow;
        var expiresAt = now + AuthPolicy.RefreshTokenLifetime;

        refreshTokens.Add(new RefreshToken
        {
            TokenHash = refreshToken.Hash,
            FamilyId = familyId,
            UserId = user.Id,
            ExpiresAtUtc = expiresAt,
            CreatedByIp = client.IpAddress,
            UserAgent = Truncate(client.UserAgent, UserAgentMaxLength),
            CreatedAtUtc = now,
        });

        var accessToken = accessTokens.IssueForCustomer(user.Id, user.SecurityStamp);
        return new AuthSession(accessToken, refreshToken.Value, expiresAt);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
