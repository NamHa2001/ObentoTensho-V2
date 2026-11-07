using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Loyalty.Services;

// Mọi thay đổi điểm đi qua đây, trong transaction của bên gọi (BR-LOY-10): khóa hàng của khách, ghi sổ cái,
// cập nhật lô FIFO, số dư, hạng, thư. Bất biến: CurrentPoints = Σ sổ cái = Σ RemainingPoints, không bao giờ âm (BR-LOY-04).
public sealed class LoyaltyLedger(
    ILoyaltyRepository loyalty,
    ILoyaltyEmailOutbox emails,
    ILoyaltySettings settings,
    IClock clock)
{
    // Cộng điểm cho một sự kiện; đã cộng cho (loại, mã tham chiếu) này rồi thì bỏ qua (BR-LOY-02). Trả số điểm đã cộng.
    // Khách đang nợ điểm thì trả nợ trước (BR-LOY-08).
    public async Task<int> EarnAsync(Guid userId, LoyaltyTransactionType type, string referenceId, int points, string? note, CancellationToken cancellationToken)
    {
        if (points <= 0 || await loyalty.EntryExistsAsync(userId, type, referenceId, cancellationToken))
        {
            return 0;
        }

        if (await loyalty.LockUserAsync(userId, cancellationToken) is not { IsDeleted: false } user)
        {
            return 0;
        }

        var now = clock.UtcNow;
        user.CurrentPoints += points;
        user.AccumulatedPoints += points;
        var earned = Add(user, points, type, referenceId, note, now);

        var repay = Math.Min(user.PointsDebt, points);
        if (repay > 0)
        {
            user.PointsDebt -= repay;
            user.CurrentPoints -= repay;
            Add(user, -repay, LoyaltyTransactionType.DebtRepaid, referenceId, "Trả nợ điểm", now);
        }

        if (points > repay)
        {
            loyalty.AddLot(new LoyaltyLot { Entry = earned, UserId = userId, RemainingPoints = points - repay, ExpiresAtUtc = now.AddMonths(settings.PointExpiryMonths) });
        }

        var tier = LoyaltyRules.Promote(user.MemberTier, user.AccumulatedPoints, settings);
        if (tier != user.MemberTier)
        {
            user.MemberTier = tier;
            emails.EnqueueTierUp(user, tier, LoyaltyRules.DiscountPercent(tier, settings));
        }

        user.UpdatedAtUtc = now;
        emails.EnqueuePointsEarned(user, points, referenceId);
        return points;
    }

    // BR-LOY-08: thu hồi đúng số điểm đã cộng cho sự kiện; đã tiêu một phần thì số dư về 0 và phần thiếu thành nợ điểm
    public async Task<int> RevokeAsync(Guid userId, LoyaltyTransactionType earnType, string referenceId, CancellationToken cancellationToken)
    {
        if (await loyalty.FindEntryAsync(userId, earnType, referenceId, cancellationToken) is not { } earned
            || await loyalty.EntryExistsAsync(userId, LoyaltyTransactionType.Revoke, referenceId, cancellationToken)
            || await loyalty.LockUserAsync(userId, cancellationToken) is not { } user)
        {
            return 0;
        }

        var take = Math.Min(user.CurrentPoints, earned.Points);
        await ConsumeAsync(userId, take, cancellationToken);
        user.CurrentPoints -= take;
        user.PointsDebt += earned.Points - take;
        user.UpdatedAtUtc = clock.UtcNow;
        Add(user, -take, LoyaltyTransactionType.Revoke, referenceId, $"Thu hồi {earned.Points} điểm", clock.UtcNow);
        return earned.Points;
    }

    // BR-LOY-11: điều chỉnh tay; trừ quá số dư thì từ chối, không cắt về 0
    public async Task<Result> AdjustAsync(LoyaltyAdjustment adjustment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        if (adjustment.Points > 0)
        {
            await EarnAsync(adjustment.UserId, LoyaltyTransactionType.AdminAdjust, adjustment.ReferenceId, adjustment.Points, adjustment.Reason, cancellationToken);
            return Result.Success();
        }

        if (await loyalty.LockUserAsync(adjustment.UserId, cancellationToken) is not { } user)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        var points = -adjustment.Points;
        if (points > user.CurrentPoints)
        {
            return Result.Failure(ErrorCodes.PointsInsufficient);
        }

        await ConsumeAsync(user.Id, points, cancellationToken);
        user.CurrentPoints -= points;
        user.UpdatedAtUtc = clock.UtcNow;
        Add(user, adjustment.Points, LoyaltyTransactionType.AdminAdjust, adjustment.ReferenceId, adjustment.Reason, clock.UtcNow);
        return Result.Success();
    }

    // BR-LOY-06: hết hạn phần còn lại của các lô quá hạn (không đổi điểm tích lũy). Trả số điểm đã hết hạn.
    public async Task<int> ExpireAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await loyalty.LockUserAsync(userId, cancellationToken) is not { } user)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var expired = (await loyalty.ListOpenLotsAsync(userId, cancellationToken)).Where(l => l.ExpiresAtUtc <= now).ToArray();
        var points = expired.Sum(l => l.RemainingPoints);
        if (points == 0)
        {
            return 0;
        }

        foreach (var lot in expired)
        {
            lot.RemainingPoints = 0;
        }

        user.CurrentPoints -= points;
        user.UpdatedAtUtc = now;
        Add(user, -points, LoyaltyTransactionType.Expired, referenceId: null, $"Hết hạn {expired.Length} lô điểm", now);
        return points;
    }

    // Tiêu điểm theo hạn sớm nhất trước; thiếu nghĩa là sổ cái lệch với số dư — dừng hẳn để transaction hoàn tác
    private async Task ConsumeAsync(Guid userId, int points, CancellationToken cancellationToken)
    {
        var left = points;
        foreach (var lot in await loyalty.ListOpenLotsAsync(userId, cancellationToken))
        {
            if (left == 0)
            {
                break;
            }

            var take = Math.Min(left, lot.RemainingPoints);
            lot.RemainingPoints -= take;
            left -= take;
        }

        if (left > 0)
        {
            throw new InvalidOperationException($"Lô điểm của khách {userId} thiếu {left} điểm so với số dư (BR-LOY-04).");
        }
    }

    private LoyaltyEntry Add(User user, int points, LoyaltyTransactionType type, string? referenceId, string? note, DateTime now)
    {
        var entry = new LoyaltyEntry
        {
            UserId = user.Id,
            Points = points,
            BalanceAfter = user.CurrentPoints,
            Type = type,
            ReferenceId = referenceId,
            Note = note,
            CreatedAtUtc = now,
        };
        loyalty.AddEntry(entry);
        return entry;
    }
}
