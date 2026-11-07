using System.Globalization;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Loyalty.Services;

// Việc định kỳ của điểm (docs/srs/13 §4.4, BR-LOY-12). Mỗi khách một transaction để một lỗi không chặn cả lượt.
public sealed class LoyaltyJobs(
    ILoyaltyRepository loyalty,
    LoyaltyLedger ledger,
    ILoyaltyEmailOutbox emails,
    ILoyaltySettings settings,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int BatchSize = 200;

    public async Task<int> ExpirePointsAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var userId in await loyalty.UsersWithExpiredLotsAsync(clock.UtcNow, BatchSize, cancellationToken))
        {
            total += await unitOfWork.InTransactionAsync(async ct =>
            {
                var expired = await ledger.ExpireAsync(userId, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return expired;
            }, cancellationToken);
        }

        return total;
    }

    // BR-LOY-12: báo một lần cho mỗi lô sắp hết hạn; gộp mọi lô của khách vào một thư
    public async Task<int> NotifyExpiringAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var until = now.AddDays(settings.ExpiryNoticeDays);
        var notified = 0;
        foreach (var userId in await loyalty.UsersToNotifyExpiryAsync(now, until, BatchSize, cancellationToken))
        {
            notified += await unitOfWork.InTransactionAsync(async ct =>
            {
                var lots = await loyalty.ListLotsToNotifyAsync(userId, now, until, ct);
                if (lots.Count == 0 || await loyalty.FindUserAsync(userId, ct) is not { IsDeleted: false } user)
                {
                    return 0;
                }

                foreach (var lot in lots)
                {
                    lot.ExpiryNoticeSentAtUtc = now;
                }

                emails.EnqueuePointsExpiring(user, lots.Sum(l => l.RemainingPoints), lots.Min(l => l.ExpiresAtUtc), now.Ticks.ToString(CultureInfo.InvariantCulture));
                await unitOfWork.SaveChangesAsync(ct);
                return 1;
            }, cancellationToken);
        }

        return notified;
    }
}
