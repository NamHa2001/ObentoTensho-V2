namespace ObentoTensho.Core.Content.Abstractions;

public interface INewsletterTokenProtector
{
    byte[] Protect(string token);

    // null: không giải mã được (mất khóa) — không gửi thư thiếu liên kết hủy
    string? Unprotect(byte[] protectedToken);
}
