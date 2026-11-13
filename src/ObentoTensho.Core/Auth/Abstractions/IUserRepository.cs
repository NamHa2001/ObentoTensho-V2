using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Abstractions;

public interface IUserRepository
{
    // Trả bản chỉ đọc; thay đổi trạng thái đăng nhập dùng các lệnh cập nhật nguyên tử bên dưới
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    // freshRead: bỏ qua bộ nhớ đệm (stamp trong token mới hơn bản đang nhớ)
    Task<UserSessionState?> GetSessionStateAsync(Guid id, CancellationToken cancellationToken, bool freshRead = false);

    // Bản được theo dõi thay đổi, lưu bằng IUnitOfWork
    Task<User?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByEmailVerifyTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    Task<User?> FindByResetTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    Task<User?> FindByPendingEmailHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListDueForDeletionAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken);

    void Add(User user);

    // Tăng bộ đếm sai trong SQL (không đọc-sửa-ghi) để các yêu cầu song song không vượt được ngưỡng khóa
    Task RegisterFailedLoginAsync(Guid id, DateTime lockoutEndUtc, CancellationToken cancellationToken);

    Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken);

    Task LockUntilAsync(Guid id, DateTime lockoutEndUtc, CancellationToken cancellationToken);

    Task ReplacePasswordHashAsync(Guid id, string currentHash, string newHash, CancellationToken cancellationToken);

    // Đặt bàn/đặt món/voucher sinh nhật chỉ cho tài khoản đang hoạt động đã xác minh SĐT
    Task<bool> IsPhoneVerifiedAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IsEmailVerifiedAsync(Guid id, CancellationToken cancellationToken);

    // Thành viên theo SĐT đã chuẩn hóa (gọi món tại bàn): ưu tiên tài khoản đã xác minh SĐT; nhiều tài khoản chưa xác minh trùng số ⇒ null
    Task<User?> FindMemberByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

    // Có tài khoản khác (chưa xóa) đã xác minh SĐT này
    Task<bool> PhoneVerifiedByOtherAsync(string phoneNumber, Guid exceptUserId, CancellationToken cancellationToken);
}
