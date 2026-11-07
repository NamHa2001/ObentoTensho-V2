using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Abstractions;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(RefreshToken token);

    // Chỉ thành công cho một yêu cầu duy nhất: token phải còn hiệu lực ngay tại câu lệnh UPDATE
    Task<bool> TryMarkRotatedAsync(long id, string replacedByTokenHash, DateTime nowUtc, CancellationToken cancellationToken);

    Task RevokeAsync(long id, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken);

    Task RevokeFamiliesAsync(IReadOnlyCollection<Guid> familyIds, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken);

    // Các phiên còn hiệu lực của khách, mới nhất trước
    Task<IReadOnlyList<Guid>> GetActiveUserFamiliesAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken);

    // FR-AUTH-09: mỗi gia đình có đúng một token còn hiệu lực, token đó tạo lúc làm mới gần nhất
    Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetActiveStaffFamiliesAsync(Guid staffId, DateTime nowUtc, CancellationToken cancellationToken);

    // Thời điểm đăng nhập của gia đình token (token đầu tiên) — mốc hạn tuyệt đối của phiên nhân viên
    Task<DateTime> GetFamilyStartedAtAsync(Guid familyId, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken);

    Task RevokeAllForStaffAsync(Guid staffId, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken);
}
