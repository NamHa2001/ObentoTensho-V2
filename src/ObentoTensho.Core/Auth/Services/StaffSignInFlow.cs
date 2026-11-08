using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// Trình tự sau khi mật khẩu đúng (docs/srs/10 §4.2):
// 2FA (nếu đã bật) → đổi mật khẩu (nếu bắt buộc) → thiết lập 2FA (nếu role bắt buộc mà chưa bật) → cấp phiên.
// 2FA đi trước đổi mật khẩu để người chỉ biết mật khẩu không tự đặt được mật khẩu mới.
public sealed class StaffSignInFlow(
    IStaffRepository staff,
    IStaffChallengeTokens challenges,
    SessionFactory sessions,
    AuthSecurityEvents securityEvents,
    IRefreshTokenRepository refreshTokens,
    IAdminEmailOutbox adminEmails,
    IAccountEmailOutbox accountEmails,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<StaffSignInResult> ContinueAsync(Staff member, bool secondFactorPassed, ClientInfo client, CancellationToken cancellationToken)
    {
        if (!secondFactorPassed && member.TotpEnabled)
        {
            return Challenge(member, StaffChallengeKind.TotpVerify, secondFactorPassed);
        }

        if (member.MustChangePassword)
        {
            return Challenge(member, StaffChallengeKind.PasswordChange, secondFactorPassed);
        }

        if (!secondFactorPassed && AuthPolicy.RequiresTwoFactor(member.Role))
        {
            return Challenge(member, StaffChallengeKind.TotpSetup, secondFactorPassed);
        }

        return StaffSignInResult.Authenticated(await StartSessionAsync(member, client, cancellationToken));
    }

    public async Task<AuthSession> StartSessionAsync(Staff member, ClientInfo client, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await AlertNewDeviceAsync(member, client, now, cancellationToken);
        await staff.ResetFailedLoginsAsync(member.Id, cancellationToken);
        await staff.RecordLoginAsync(member.Id, now, cancellationToken);
        await sessions.EnforceStaffSessionLimitAsync(member.Id, cancellationToken);
        var session = sessions.CreateForStaff(member, Guid.CreateVersion7(), now, sessions.NewRefreshToken(), client);
        securityEvents.RecordStaffLoginSucceeded(member);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }

    // SEC-58: đăng nhập quản trị từ trình duyệt/dải mạng chưa từng dùng ⇒ báo chính nhân viên đó kèm liên kết "Không phải tôi";
    // SuperAdmin thì báo thêm các SuperAdmin khác. Lần đăng nhập đầu tiên (vừa nhận lời mời) không báo
    private async Task AlertNewDeviceAsync(Staff member, ClientInfo client, DateTime now, CancellationToken cancellationToken)
    {
        var prefix = ClientNetwork.Prefix(client.IpAddress);
        var userAgent = client.UserAgent is { Length: > SessionFactory.UserAgentMaxLength } ua ? ua[..SessionFactory.UserAgentMaxLength] : client.UserAgent;
        if (!await refreshTokens.StaffHasSessionsAsync(member.Id, cancellationToken)
            || await refreshTokens.StaffSeenClientAsync(member.Id, prefix, userAgent, cancellationToken))
        {
            return;
        }

        var notMe = challenges.Protect(new StaffChallenge(member.Id, StaffChallengeKind.NotMe, member.SecurityStamp, now + AuthPolicy.NotMeLinkLifetime, SecondFactorPassed: false));
        var recipient = new AccountRecipient(member.Id, member.Email, member.FullName, AuthPolicy.DefaultLanguage, IsStaff: true);
        accountEmails.EnqueueStaffNewDevice(recipient, client with { UserAgent = userAgent }, now, notMe);
        if (member.Role == StaffRole.SuperAdmin)
        {
            var message = $"Tài khoản quản trị {member.Email} vừa đăng nhập từ thiết bị hoặc mạng mới (IP {client.IpAddress ?? "không rõ"}, "
                + $"trình duyệt: {userAgent ?? "không rõ"}).";
            var key = $"new-device:{member.Id:N}:{now.Ticks}";
            foreach (var email in (await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin], cancellationToken))
                .Where(e => !string.Equals(e, member.Email, StringComparison.OrdinalIgnoreCase)))
            {
                adminEmails.EnqueueAlert(email, message, key);
            }
        }
    }

    // Mã tạm chỉ hợp lệ khi đúng loại, còn hạn, tài khoản còn hoạt động, không bị khóa và security stamp chưa đổi
    public async Task<(Staff Member, StaffChallenge Challenge)?> ResolveAsync(string? token, StaffChallengeKind kind, CancellationToken cancellationToken)
    {
        var challenge = string.IsNullOrEmpty(token) ? null : challenges.Unprotect(token);
        var now = clock.UtcNow;
        if (challenge is null || challenge.Kind != kind || challenge.ExpiresAtUtc <= now)
        {
            return null;
        }

        var member = await staff.FindByIdAsync(challenge.StaffId, cancellationToken);
        if (member is null || !member.IsActive || member.LockoutEndUtc > now
            || !string.Equals(member.SecurityStamp, challenge.SecurityStamp, StringComparison.Ordinal))
        {
            return null;
        }

        return (member, challenge);
    }

    private StaffSignInResult Challenge(Staff member, StaffChallengeKind kind, bool secondFactorPassed)
    {
        var expiresAt = clock.UtcNow + AuthPolicy.StaffChallengeLifetime;
        var token = challenges.Protect(new StaffChallenge(member.Id, kind, member.SecurityStamp, expiresAt, secondFactorPassed));
        return StaffSignInResult.Challenged(new IssuedChallenge(kind, token, expiresAt));
    }
}
