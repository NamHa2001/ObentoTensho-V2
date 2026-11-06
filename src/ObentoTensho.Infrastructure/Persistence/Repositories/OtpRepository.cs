using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Verification.Abstractions;
using ObentoTensho.Core.Verification.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class OtpRepository(AppDbContext db) : IOtpRepository
{
    public void Add(OtpChallenge challenge) => db.OtpChallenges.Add(challenge);

    public Task<DateTime?> LastSentAtAsync(OtpPurpose purpose, string targetHash, CancellationToken cancellationToken) =>
        db.OtpChallenges.Where(c => c.Purpose == purpose && c.TargetHash == targetHash)
            .MaxAsync(c => (DateTime?)c.CreatedAtUtc, cancellationToken);

    public Task<int> CountForTargetSinceAsync(string targetHash, DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.OtpChallenges.CountAsync(c => c.TargetHash == targetHash && c.CreatedAtUtc >= sinceUtc, cancellationToken);

    public Task<int> CountForIpSinceAsync(string ipHash, DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.OtpChallenges.CountAsync(c => c.IpHash == ipHash && c.CreatedAtUtc >= sinceUtc, cancellationToken);

    public Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.OtpChallenges.CountAsync(c => c.CreatedAtUtc >= sinceUtc, cancellationToken);

    public Task<OtpChallenge?> FindUsableForUpdateAsync(OtpPurpose purpose, string targetHash, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.OtpChallenges
            .Where(c => c.Purpose == purpose && c.TargetHash == targetHash && c.VerifiedAtUtc == null && c.ExpiresAtUtc > nowUtc && c.Attempts < OtpPolicy.MaxAttempts)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
