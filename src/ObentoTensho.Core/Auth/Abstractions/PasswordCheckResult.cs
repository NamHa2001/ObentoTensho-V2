namespace ObentoTensho.Core.Auth.Abstractions;

public enum PasswordCheckResult
{
    Failed,
    Success,

    // Mật khẩu đúng nhưng được băm bằng thông số cũ: băm lại ngay sau khi đăng nhập
    SuccessRehashNeeded,
}
