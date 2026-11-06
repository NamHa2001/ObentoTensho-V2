using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Auth.Abstractions;

// Email ghi vào hộp thư đi cùng transaction với thay đổi dữ liệu, gửi sau khi commit (DB-25)
public interface IAccountEmailOutbox
{
    void EnqueueEmailVerification(User user, string verificationToken);

    // D-13: báo chủ tài khoản khi có người đăng ký bằng email của họ; tối đa một email mỗi ngày
    void EnqueueDuplicateRegistrationNotice(User existingUser, DateOnly localDate);
}
