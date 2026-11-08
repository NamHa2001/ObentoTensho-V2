namespace ObentoTensho.Core.Auth.Abstractions;

// BR-AUTH-16: email dùng một lần không nhận ưu đãi (voucher sinh nhật/chào mừng); vẫn đăng ký và đặt món được
public interface IDisposableEmailDomains
{
    bool IsDisposable(string email);
}
