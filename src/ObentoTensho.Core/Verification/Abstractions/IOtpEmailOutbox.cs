namespace ObentoTensho.Core.Verification.Abstractions;

// Thư mã xác minh (#34) — ưu tiên nhóm 1 của hạn mức email (BR-NTF-12)
public interface IOtpEmailOutbox
{
    void EnqueueCode(string email, string language, string code, Guid challengeKey);
}
