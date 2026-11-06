using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Core.Auth.Services;

// SEC-50: access token chỉ còn hiệu lực khi tài khoản còn hoạt động và security stamp (claim "sv") chưa đổi,
// nên khóa tài khoản/đổi mật khẩu có hiệu lực ngay, không chờ token hết hạn
public sealed class AccessTokenSessionValidator(IUserRepository users, IStaffRepository staff)
{
    public async Task<bool> IsCustomerSessionValidAsync(Guid userId, string? securityStamp, CancellationToken cancellationToken) =>
        !string.IsNullOrEmpty(securityStamp) && Matches(await users.GetSessionStateAsync(userId, cancellationToken), securityStamp);

    public async Task<bool> IsStaffSessionValidAsync(Guid staffId, string? securityStamp, CancellationToken cancellationToken) =>
        !string.IsNullOrEmpty(securityStamp) && Matches(await staff.GetSessionStateAsync(staffId, cancellationToken), securityStamp);

    private static bool Matches(UserSessionState? state, string securityStamp) =>
        state is { CanSignIn: true } && string.Equals(state.SecurityStamp, securityStamp, StringComparison.Ordinal);
}
