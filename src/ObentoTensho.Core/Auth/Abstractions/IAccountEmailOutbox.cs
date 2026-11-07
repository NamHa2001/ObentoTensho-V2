using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Abstractions;

// Email ghi vào hộp thư đi cùng transaction với thay đổi dữ liệu, gửi sau khi commit (DB-25, BR-NTF-02)
public interface IAccountEmailOutbox
{
    void EnqueueEmailVerification(User user, string verificationToken);

    // D-13: báo chủ tài khoản khi có người đăng ký bằng email của họ; tối đa một email mỗi ngày
    void EnqueueDuplicateRegistrationNotice(User existingUser, DateOnly localDate);

    void EnqueuePasswordReset(AccountRecipient recipient, string resetToken);

    // BR-AUTH-15: liên kết xác minh gửi tới email mới, thông báo gửi tới email cũ
    void EnqueueEmailChangeVerify(AccountRecipient recipient, string newEmail, string token);

    void EnqueueEmailChangeNotice(AccountRecipient recipient, DateTime atUtc);

    // BR-CUS-05
    void EnqueueAccountDeletionScheduled(AccountRecipient recipient, DateTime scheduledAtUtc);

    // BR-CUS-08
    void EnqueueAccountLocked(AccountRecipient recipient, DateTime atUtc);

    void EnqueueAccountUnlocked(AccountRecipient recipient, DateTime atUtc);

    // BR-STF-06: liên kết mời một lần, không chứa mật khẩu
    void EnqueueStaffInvite(AccountRecipient recipient, string inviteToken);

    // Thư cảnh báo bảo mật (#23): mật khẩu vừa được đổi/đặt lại
    void EnqueuePasswordChanged(AccountRecipient recipient, DateTime changedAtUtc);
}
