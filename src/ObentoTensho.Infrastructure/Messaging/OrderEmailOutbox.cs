using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// BR-NTF-04: thư có mã đơn, giờ lấy theo giờ VN, món + tùy chọn theo ngôn ngữ khách; liên kết theo dõi đặt token sau #fragment (SEC-46)
internal sealed class OrderEmailOutbox(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : IOrderEmailOutbox
{
    public void EnqueueReceived(Order order, string accessToken) =>
        Enqueue(EmailKind.OrderReceived, order, ("trackLink", TrackLink(order, accessToken)));

    public void EnqueueReady(Order order) =>
        Enqueue(EmailKind.OrderReady, order, ("trackLink", TrackLink(order, accessToken: null)));

    public void EnqueueCancelled(Order order, string reason) =>
        Enqueue(EmailKind.OrderCancelled, order, ("reason", reason), ("menuLink", MenuLink()));

    public void EnqueueCancelledByCustomer(Order order) =>
        Enqueue(EmailKind.OrderCancelledByCustomer, order, ("menuLink", MenuLink()));

    public void EnqueueRefunded(Order order) =>
        Enqueue(EmailKind.OrderRefunded, order, ("refundNote", order.RefundNote ?? string.Empty));

    private string MenuLink() => links.Value.PublicBaseUrl.TrimEnd('/') + "/menu";

    // Có token: mở thẳng trang theo dõi; không có (thư sau): trang tra cứu bằng mã + 4 số cuối SĐT
    private string TrackLink(Order order, string? accessToken)
    {
        var baseUrl = links.Value.PublicBaseUrl.TrimEnd('/');
        var code = Uri.EscapeDataString(order.OrderCode);
        return accessToken is null
            ? $"{baseUrl}/orders/lookup?code={code}"
            : $"{baseUrl}/orders/track?code={code}#token={Uri.EscapeDataString(accessToken)}";
    }

    private void Enqueue(EmailKind kind, Order order, params (string Key, string Value)[] extra)
    {
        var pickup = clock.ToLocal(order.PickupAtUtc);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fullName"] = order.CustomerName,
            ["code"] = order.OrderCode,
            ["date"] = pickup.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["time"] = pickup.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["items"] = Lines(order),
            ["total"] = Money(order.TotalAmount),
            ["payment"] = PaymentText(order.PaymentMethod, order.Lang),
        };
        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        var now = clock.UtcNow;
        var request = new EmailRequest(kind, order.CustomerEmail, order.Lang, values, $"{EmailTypes.Of(kind)}:{order.Id:N}", order.Id.ToString(), Attachments: null);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = AccountEmailOutbox.EmailType,
            PayloadJson = JsonSerializer.Serialize(request, OutboxJson.Options),
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
        });
    }

    // BR-ORD-23: mỗi món một dòng, tùy chọn ngay sau tên, theo ngôn ngữ khách lúc đặt
    private static string Lines(Order order)
    {
        var text = new StringBuilder();
        foreach (var item in order.Items)
        {
            var options = item.Options.Select(o => o.OptionNameLocalized ?? o.OptionName).ToArray();
            text.Append(CultureInfo.InvariantCulture, $"{item.Quantity} × {item.ItemNameLocalized ?? item.ItemName}");
            if (options.Length > 0)
            {
                text.Append(CultureInfo.InvariantCulture, $" ({string.Join(", ", options)})");
            }

            text.Append(CultureInfo.InvariantCulture, $" — {Money(item.LineTotal)}").Append('\n');
        }

        return text.ToString().TrimEnd();
    }

    private static string Money(decimal amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", ".", StringComparison.Ordinal) + " ₫";

    private static string PaymentText(PaymentMethod method, string lang) => (method, lang) switch
    {
        (PaymentMethod.BankTransfer, "en") => "Bank transfer",
        (PaymentMethod.BankTransfer, "ja") => "銀行振込",
        (PaymentMethod.BankTransfer, _) => "Chuyển khoản",
        (_, "en") => "Cash on pickup",
        (_, "ja") => "受け取り時に現金",
        _ => "Tiền mặt khi nhận món",
    };
}
