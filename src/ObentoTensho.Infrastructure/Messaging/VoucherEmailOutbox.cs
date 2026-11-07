using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;
using ObentoTensho.Core.Vouchers.Services;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// docs/srs/20 #17: thư mã voucher sau khi đổi điểm, theo ngôn ngữ ưa thích của thành viên
internal sealed class VoucherEmailOutbox(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : IVoucherEmailOutbox
{
    public void EnqueueRedeemed(User user, Voucher voucher, int points)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(voucher);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fullName"] = user.FullName,
            ["voucherCode"] = voucher.Code,
            ["value"] = voucher.DiscountValue.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", ".", StringComparison.Ordinal) + " ₫",
            ["points"] = points.ToString(CultureInfo.InvariantCulture),
            ["expiryDate"] = VoucherMappings.EndDate(voucher, clock)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
            ["accountLink"] = links.Value.PublicBaseUrl.TrimEnd('/') + "/account/vouchers",
        };

        var now = clock.UtcNow;
        var request = new EmailRequest(EmailKind.VoucherRedeemed, user.Email, user.PreferredLang, values, $"{EmailTypes.Of(EmailKind.VoucherRedeemed)}:{voucher.Code}", user.Id.ToString(), Attachments: null);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = AccountEmailOutbox.EmailType,
            PayloadJson = JsonSerializer.Serialize(request, OutboxJson.Options),
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
        });
    }
}
