using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Loyalty.Abstractions;

// BR-LOY-12, docs/srs/20 #14, #15, #22: thư cho thành viên, ghi cùng transaction với sổ cái
public interface ILoyaltyEmailOutbox
{
    void EnqueuePointsEarned(User user, int points, string referenceId);

    void EnqueueTierUp(User user, MemberTier tier, int discountPercent);

    void EnqueuePointsExpiring(User user, int points, DateTime expiresAtUtc, string batchKey);
}
