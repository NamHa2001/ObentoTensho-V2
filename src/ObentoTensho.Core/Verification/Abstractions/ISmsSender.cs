namespace ObentoTensho.Core.Verification.Abstractions;

// Kênh SMS cho OTP số điện thoại (BR-AUTH-18: đổi nhà cung cấp không sửa nghiệp vụ). Số ở dạng +84…
public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken);
}
