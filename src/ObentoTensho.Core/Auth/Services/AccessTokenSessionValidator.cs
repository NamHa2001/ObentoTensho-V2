using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Core.Auth.Services;

// SEC-50: access token chỉ còn hiệu lực khi tài khoản còn hoạt động và security stamp (claim "sv") chưa đổi,
// nên khóa tài khoản/đổi mật khẩu có hiệu lực ngay, không chờ token hết hạn
public sealed class AccessTokenSessionValidator(IUserRepository users)
{
    public async Task<bool> IsCustomerSessionValidAsync(Guid userId, string? securityStamp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(securityStamp))
        {
            return false;
        }

        var state = await users.GetSessionStateAsync(userId, cancellationToken);
        return state is { CanSignIn: true } && string.Equals(state.SecurityStamp, securityStamp, StringComparison.Ordinal);
    }
}
