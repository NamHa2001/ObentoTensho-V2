namespace ObentoTensho.Core.Notifications.Domain;

// Tên loại email lưu ở EmailLogs.Type và dùng làm tên tệp mẫu
public static class EmailTypes
{
    public static string Of(EmailKind kind) => kind switch
    {
        EmailKind.VerifyEmail => "verify_email",
        EmailKind.DuplicateRegistration => "duplicate_registration",
        EmailKind.PasswordReset => "password_reset",
        EmailKind.PasswordChanged => "password_changed",
        EmailKind.ReservationReceived => "reservation_received",
        EmailKind.ReservationConfirmed => "reservation_confirmed",
        EmailKind.ReservationRejected => "reservation_rejected",
        EmailKind.ReservationCancelled => "reservation_cancelled",
        EmailKind.ReservationReminder => "reservation_reminder",
        EmailKind.ReservationAutoCancelled => "reservation_auto_cancelled",
        EmailKind.VerificationCode => "verification_code",
        EmailKind.OrderReceived => "order_received",
        EmailKind.OrderReady => "order_ready",
        EmailKind.OrderCancelled => "order_cancelled",
        EmailKind.OrderCancelledByCustomer => "order_cancelled_by_customer",
        EmailKind.OrderRefunded => "order_refunded",
        EmailKind.OrderAttention => "order_attention",
        EmailKind.LoyaltyPointsEarned => "loyalty_points_earned",
        EmailKind.LoyaltyTierUp => "loyalty_tier_up",
        EmailKind.LoyaltyPointsExpiring => "loyalty_points_expiring",
        EmailKind.VoucherRedeemed => "voucher_redeemed",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Loại email chưa có tên."),
    };

    public static IReadOnlyList<string> SecurityNotices { get; } =
        Enum.GetValues<EmailKind>().Where(EmailPolicy.IsSecurityNotice).Select(Of).ToArray();
}
