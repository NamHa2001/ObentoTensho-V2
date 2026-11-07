using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Loyalty;

/// <summary>
/// Số dư điểm và hạng (docs/srs/13 §7). <c>PointsDebt</c>: nợ điểm sau khi bị thu hồi (BR-LOY-08), trả dần bằng lần tích sau.
/// <c>NextTier</c>/<c>PointsToNextTier</c> null khi đã ở hạng cao nhất. <c>ExpiringPoints</c>: điểm hết hạn trong 30 ngày tới.
/// </summary>
public sealed record LoyaltySummaryDto(
    int CurrentPoints,
    int AccumulatedPoints,
    int PointsDebt,
    MemberTier Tier,
    int DiscountPercent,
    MemberTier? NextTier,
    int? PointsToNextTier,
    int ExpiringPoints,
    DateTime? NextExpiryAtUtc);
