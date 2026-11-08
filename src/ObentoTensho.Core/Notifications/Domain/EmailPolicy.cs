namespace ObentoTensho.Core.Notifications.Domain;

// BR-NTF-08 (giới hạn theo địa chỉ nhận), BR-NTF-09 (cảnh báo bảo mật), BR-NTF-12 (hạn mức theo ưu tiên).
// Q1: chia ba nhóm thay vì bỏ mọi thư vượt giới hạn —
//   thư giao dịch cho chính chủ (xác nhận, OTP, đặt lại mật khẩu, cảnh báo bảo mật): không giới hạn theo giờ, chỉ trần 30/ngày chống spam;
//   thư nội bộ cho nhân viên (đã là bản tóm tắt/cảnh báo): không giới hạn theo địa chỉ;
//   thư quảng bá (điểm, voucher, bản tin): giữ 3 cùng loại/giờ và 10/ngày, vượt thì dời sang ngày sau
public static class EmailPolicy
{
    public const int MaxSameKindPerHour = 3;
    public const int MaxPerDay = 10;
    public const int MaxTransactionalPerDay = 30;

    // Từ mức này trở lên chỉ gửi thư ưu tiên cao
    public const double SoftQuotaRatio = 0.8;

    // 1 = xác thực/đặt lại mật khẩu/lời mời … 5 = bản tin
    public static int Priority(EmailKind kind) => kind switch
    {
        EmailKind.VerifyEmail or EmailKind.PasswordReset or EmailKind.VerificationCode or EmailKind.StaffInvite or EmailKind.EmailChangeVerify
            or EmailKind.StaffNewDevice => 1,
        EmailKind.ReservationReceived or EmailKind.ReservationConfirmed or EmailKind.ReservationRejected
            or EmailKind.ReservationCancelled or EmailKind.ReservationAutoCancelled
            or EmailKind.OrderReceived or EmailKind.OrderReady or EmailKind.OrderCancelled
            or EmailKind.OrderCancelledByCustomer or EmailKind.OrderRefunded => 2,
        EmailKind.ReservationReminder or EmailKind.OrderAttention or EmailKind.ContactReceived or EmailKind.ContactInternal or EmailKind.NewsletterConfirm
            or EmailKind.NewsletterAlreadySubscribed
            or EmailKind.AccountLocked or EmailKind.AccountUnlocked or EmailKind.AdminAlert or EmailKind.DataExportReady => 3,
        EmailKind.DuplicateRegistration or EmailKind.PasswordChanged or EmailKind.EmailChangeNotice or EmailKind.AccountDeletionScheduled or EmailKind.LoginLockout => 3,
        EmailKind.LoyaltyPointsEarned or EmailKind.LoyaltyTierUp or EmailKind.LoyaltyPointsExpiring or EmailKind.VoucherRedeemed or EmailKind.VoucherBirthday => 4,
        _ => 5,
    };

    public static bool IsInternal(EmailKind kind) =>
        kind is EmailKind.OrderAttention or EmailKind.ContactInternal or EmailKind.AdminAlert;

    public static bool IsPromotional(EmailKind kind) => !IsInternal(kind) && Priority(kind) >= 4;

    public static RecipientLimit CheckRecipient(EmailKind kind, int sameKindLastHour, int allKindsToday)
    {
        if (IsInternal(kind))
        {
            return RecipientLimit.Within;
        }

        if (IsPromotional(kind))
        {
            return sameKindLastHour >= MaxSameKindPerHour || allKindsToday >= MaxPerDay ? RecipientLimit.Defer : RecipientLimit.Within;
        }

        return allKindsToday >= MaxTransactionalPerDay ? RecipientLimit.Suppress : RecipientLimit.Within;
    }

    // Hết hạn mức: nhóm 1–2 vẫn gửi (thư đăng nhập/xác nhận không được treo); ≥ 80%: hoãn nhóm 4–5; 100%: hoãn nhóm 3+
    public static bool ShouldDefer(EmailKind kind, int sentToday, int dailyQuota)
    {
        var priority = Priority(kind);
        if (sentToday >= dailyQuota)
        {
            return priority >= 3;
        }

        return sentToday >= dailyQuota * SoftQuotaRatio && priority >= 4;
    }
}
