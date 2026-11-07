using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Abstractions;

public interface IStaffRepository
{
    // Bản chỉ đọc
    Task<Staff?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Staff?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    // Bản được theo dõi thay đổi, lưu bằng IUnitOfWork
    Task<Staff?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<Staff?> FindByInviteTokenHashForUpdateAsync(string inviteTokenHash, CancellationToken cancellationToken);

    Task<Staff?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<Staff?> FindByResetTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    Task<bool> AnyWithRoleAsync(StaffRole role, CancellationToken cancellationToken);

    // Email nhân viên đang hoạt động thuộc các vai trò (nhận thư vận hành)
    Task<IReadOnlyList<string>> ListActiveEmailsAsync(IReadOnlyCollection<StaffRole> roles, CancellationToken cancellationToken);

    Task<UserSessionState?> GetSessionStateAsync(Guid id, CancellationToken cancellationToken);

    void Add(Staff staff);

    Task RegisterFailedLoginAsync(Guid id, DateTime lockoutEndUtc, CancellationToken cancellationToken);

    Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken);

    Task ReplacePasswordHashAsync(Guid id, string currentHash, string newHash, CancellationToken cancellationToken);

    Task RecordLoginAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken);

    // Chỉ một yêu cầu dùng được mỗi bước TOTP, kể cả khi gửi song song
    Task<bool> TryConsumeTotpStepAsync(Guid id, long timeStep, CancellationToken cancellationToken);

    Task ReplaceRecoveryCodesAsync(Guid staffId, IEnumerable<string> codeHashes, CancellationToken cancellationToken);

    Task<bool> TryUseRecoveryCodeAsync(Guid staffId, string codeHash, DateTime nowUtc, CancellationToken cancellationToken);
}
