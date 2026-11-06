using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Verification.Domain;

namespace ObentoTensho.Core.Verification.Abstractions;

public interface IOtpRepository
{
    void Add(OtpChallenge challenge);

    Task<DateTime?> LastSentAtAsync(OtpPurpose purpose, string targetHash, CancellationToken cancellationToken);

    Task<int> CountForTargetSinceAsync(string targetHash, DateTime sinceUtc, CancellationToken cancellationToken);

    Task<int> CountForIpSinceAsync(string ipHash, DateTime sinceUtc, CancellationToken cancellationToken);

    Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken);

    // Mã mới nhất còn dùng được (chưa xác minh, chưa hết hạn, chưa sai quá số lần) — bản được theo dõi để sửa
    Task<OtpChallenge?> FindUsableForUpdateAsync(OtpPurpose purpose, string targetHash, DateTime nowUtc, CancellationToken cancellationToken);
}
