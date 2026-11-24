using ObentoTensho.Contracts.Dtos.Loyalty;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Domain;

namespace ObentoTensho.Core.Loyalty.Services;

public static class LoyaltyMappings
{
    public static LoyaltySummaryDto ToSummary(this User user, ILoyaltySettings settings, int expiringPoints, DateTime? nextExpiryUtc)
    {
        ArgumentNullException.ThrowIfNull(user);
        var next = LoyaltyRules.Next(user.MemberTier, settings);
        return new LoyaltySummaryDto(
            user.CurrentPoints,
            user.AccumulatedPoints,
            user.PointsDebt,
            user.MemberTier,
            LoyaltyRules.DiscountPercent(user.MemberTier, settings),
            next?.Tier,
            next is { } n ? Math.Max(0, n.Points - user.AccumulatedPoints) : null,
            expiringPoints,
            nextExpiryUtc);
    }

    public static LoyaltyEntryDto ToDto(this LoyaltyEntry e) =>
        new(e.Id, e.Points, e.BalanceAfter, e.Type, e.ReferenceId, e.Note, e.CreatedAtUtc);

    public static LoyaltyAdjustmentDto ToDto(this LoyaltyAdjustment a, string? customerName = null) =>
        new(a.Id, a.UserId, a.Points, a.Reason, a.Status, a.RequestedByStaffId, a.RequestedAtUtc, a.ReviewedByStaffId, a.ReviewedAtUtc, a.ReviewNote, customerName);
}
