namespace ObentoTensho.Core.Auth.Abstractions;

// SEC-50: đăng xuất/thu hồi thiết bị/thu hồi mọi phiên ⇒ access token còn hạn của phiên đó bị từ chối ngay,
// không chờ hết 15 phút. Chỉ cần nhớ trong thời gian sống của access token
public interface ISessionDenylist
{
    // Thời gian một access token còn có thể được chấp nhận (đời token + sai lệch đồng hồ)
    TimeSpan Lifetime { get; }

    void RevokeSession(Guid sessionId);

    bool IsRevoked(Guid sessionId);
}
