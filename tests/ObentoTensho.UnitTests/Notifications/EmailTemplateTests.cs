using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Infrastructure.Email;

namespace ObentoTensho.UnitTests.Notifications;

// docs/srs/20 §6: mọi loại email render được đủ 3 ngôn ngữ, có text thuần, không sót biến
public sealed class EmailTemplateTests
{
    private static readonly Dictionary<string, string> AllValues = new(StringComparer.Ordinal)
    {
        ["fullName"] = "Nguyễn Văn An",
        ["link"] = "https://www.obento.test/auth/x#token=abc",
        ["loginLink"] = "https://www.obento.test/auth/login",
        ["forgotLink"] = "https://www.obento.test/auth/forgot-password",
        ["changedAt"] = "09:30 07/11/2026",
        ["code"] = "OBT-8K4Q-7ZM2",
        ["date"] = "08/11/2026",
        ["time"] = "18:30",
        ["guests"] = "4",
        ["manageLink"] = "https://www.obento.test/reservations/manage?code=OBT-8K4Q-7ZM2#token=abc",
        ["bookingLink"] = "https://www.obento.test/reservations",
        ["reason"] = "Ca đã kín chỗ",
        ["minutes"] = "5",
        ["items"] = "2 × Cơm gà (Lớn) — 160.000 ₫\n1 × Trà đá — 5.000 ₫",
        ["total"] = "165.000 ₫",
        ["payment"] = "Tiền mặt khi nhận món",
        ["trackLink"] = "https://www.obento.test/orders/track?code=OBT-TK-8K4Q-7ZM2#token=abc",
        ["menuLink"] = "https://www.obento.test/menu",
        ["refundNote"] = "Đã chuyển khoản lại",
        ["pendingCount"] = "1",
        ["notPickedUpCount"] = "0",
        ["refundCount"] = "0",
        ["pendingList"] = "OBT-TK-8K4Q-7ZM2 — 12:00 08/11 — Nguyễn Văn An — 165.000 ₫",
        ["notPickedUpList"] = "—",
        ["refundList"] = "—",
        ["adminLink"] = "https://admin.obento.test/orders/attention",
    };

    public static TheoryData<EmailKind, string> Cases()
    {
        var data = new TheoryData<EmailKind, string>();
        foreach (var kind in Enum.GetValues<EmailKind>())
        {
            foreach (var language in new[] { "vi", "en", "ja" })
            {
                data.Add(kind, language);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Moi_loai_moi_ngon_ngu_render_day_du(EmailKind kind, string language)
    {
        var message = new EmbeddedEmailTemplateRenderer().Render(new EmailRequest(kind, "an@example.com", language, AllValues, "k", null));

        Assert.False(string.IsNullOrWhiteSpace(message.Subject));
        Assert.DoesNotContain('\n', message.Subject);
        Assert.False(string.IsNullOrWhiteSpace(message.TextBody));
        Assert.DoesNotContain("{{", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"<html lang=\"{language}\">", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Du_12_mau_va_ngon_ngu_la_roi_ve_tieng_Viet()
    {
        Assert.Equal(Enum.GetValues<EmailKind>().Length * 3, EmbeddedEmailTemplateRenderer.Keys.Count);

        var message = new EmbeddedEmailTemplateRenderer().Render(new EmailRequest(EmailKind.PasswordReset, "an@example.com", "fr", AllValues, "k", null));
        Assert.Equal("Đặt lại mật khẩu Obento Tensho", message.Subject);
    }

    [Fact]
    public void Thieu_bien_thi_bao_loi_khong_gui_thu_hong()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["fullName"] = "An" };

        Assert.Throws<InvalidOperationException>(() =>
            new EmbeddedEmailTemplateRenderer().Render(new EmailRequest(EmailKind.VerifyEmail, "an@example.com", "vi", values, "k", null)));
    }

    [Theory]
    [InlineData(EmailKind.VerifyEmail, 299, 300, false)]
    [InlineData(EmailKind.VerifyEmail, 300, 300, false)]
    [InlineData(EmailKind.PasswordChanged, 299, 300, false)]
    [InlineData(EmailKind.PasswordChanged, 300, 300, true)]
    public void Het_han_muc_van_gui_thu_uu_tien_cao_hoan_thu_thap(EmailKind kind, int sentToday, int quota, bool deferred)
    {
        Assert.Equal(deferred, EmailPolicy.ShouldDefer(kind, sentToday, quota));
    }

    [Fact]
    public void Gioi_han_theo_dia_chi_nhan()
    {
        Assert.False(EmailPolicy.ExceedsRecipientLimit(EmailKind.PasswordReset, sameKindLastHour: 2, allKindsToday: 2, securityNoticesToday: 0));
        Assert.True(EmailPolicy.ExceedsRecipientLimit(EmailKind.PasswordReset, sameKindLastHour: 3, allKindsToday: 3, securityNoticesToday: 0));
        Assert.True(EmailPolicy.ExceedsRecipientLimit(EmailKind.VerifyEmail, sameKindLastHour: 0, allKindsToday: 10, securityNoticesToday: 0));

        // Cảnh báo bảo mật cho chính chủ: không theo giới hạn thường, chỉ trần 30/ngày
        Assert.False(EmailPolicy.ExceedsRecipientLimit(EmailKind.PasswordChanged, sameKindLastHour: 5, allKindsToday: 12, securityNoticesToday: 29));
        Assert.True(EmailPolicy.ExceedsRecipientLimit(EmailKind.PasswordChanged, sameKindLastHour: 5, allKindsToday: 12, securityNoticesToday: 30));
    }
}
