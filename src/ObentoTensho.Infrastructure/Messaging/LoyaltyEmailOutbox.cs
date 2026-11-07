using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// BR-LOY-12: thư điểm cho thành viên theo ngôn ngữ ưa thích; liên kết về trang điểm của tài khoản
internal sealed class LoyaltyEmailOutbox(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : ILoyaltyEmailOutbox
{
    public void EnqueuePointsEarned(User user, int points, string referenceId)
    {
        ArgumentNullException.ThrowIfNull(user);
        Enqueue(EmailKind.LoyaltyPointsEarned, user, $"{referenceId}", ("points", Number(points)), ("balance", Number(user.CurrentPoints)), ("reference", referenceId));
    }

    public void EnqueueTierUp(User user, MemberTier tier, int discountPercent)
    {
        ArgumentNullException.ThrowIfNull(user);
        Enqueue(EmailKind.LoyaltyTierUp, user, tier.ToString(), ("tierName", TierName(tier, user.PreferredLang)), ("discount", discountPercent.ToString(CultureInfo.InvariantCulture)));
    }

    public void EnqueuePointsExpiring(User user, int points, DateTime expiresAtUtc, string batchKey)
    {
        ArgumentNullException.ThrowIfNull(user);
        var date = clock.ToLocal(expiresAtUtc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        Enqueue(EmailKind.LoyaltyPointsExpiring, user, batchKey, ("points", Number(points)), ("expiryDate", date));
    }

    private void Enqueue(EmailKind kind, User user, string key, params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fullName"] = user.FullName,
            ["accountLink"] = links.Value.PublicBaseUrl.TrimEnd('/') + "/account/points",
        };
        foreach (var (name, value) in extra)
        {
            values[name] = value;
        }

        var now = clock.UtcNow;
        var request = new EmailRequest(kind, user.Email, user.PreferredLang, values, $"{EmailTypes.Of(kind)}:{user.Id:N}:{key}", user.Id.ToString(), Attachments: null);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = AccountEmailOutbox.EmailType,
            PayloadJson = JsonSerializer.Serialize(request, OutboxJson.Options),
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
        });
    }

    private static string Number(int value) =>
        value.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", ".", StringComparison.Ordinal);

    private static string TierName(MemberTier tier, string lang) => (tier, lang) switch
    {
        (MemberTier.Silver, "en") => "Silver",
        (MemberTier.Gold, "en") => "Gold",
        (MemberTier.Diamond, "en") => "Diamond",
        (MemberTier.Silver, "ja") => "シルバー",
        (MemberTier.Gold, "ja") => "ゴールド",
        (MemberTier.Diamond, "ja") => "ダイヤモンド",
        (MemberTier.Silver, _) => "Bạc",
        (MemberTier.Gold, _) => "Vàng",
        (MemberTier.Diamond, _) => "Kim cương",
        _ => "Đồng",
    };
}
