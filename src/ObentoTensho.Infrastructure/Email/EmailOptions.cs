using System.Net.Mail;

namespace ObentoTensho.Infrastructure.Email;

// BR-NTF-07, BR-NTF-10, BR-NTF-11: mật khẩu SMTP qua biến môi trường/secret, không nằm trong repo
public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public const string SmtpMode = "smtp";
    public const string PickupMode = "pickup";

    // smtp: gửi thật qua nhà cung cấp (Brevo/Mailgun/SES…); pickup: ghi tệp .eml vào thư mục (máy dev)
    public string Mode { get; set; } = PickupMode;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Obento Tensho";

    public string? ReplyTo { get; set; }

    public string PickupDirectory { get; set; } = "mail-pickup";

    // BR-NTF-12 (`email.dailyQuota`): đặt bằng hạn mức thật của gói gửi thư
    public int DailyQuota { get; set; } = 300;

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    // Trả null khi hợp lệ; môi trường Production bắt buộc gửi thật qua SMTP
    public string? Validate(bool isProduction)
    {
        if (!IsEmail(FromAddress) || (ReplyTo is not null && !IsEmail(ReplyTo)))
        {
            return "Email:FromAddress / Email:ReplyTo không hợp lệ.";
        }

        if (DailyQuota is < 10 or > 100_000)
        {
            return "Email:DailyQuota phải từ 10 đến 100000.";
        }

        if (string.Equals(Mode, PickupMode, StringComparison.Ordinal))
        {
            return isProduction ? "Production phải dùng Email:Mode = smtp." : null;
        }

        if (!string.Equals(Mode, SmtpMode, StringComparison.Ordinal))
        {
            return "Email:Mode phải là smtp hoặc pickup.";
        }

        return string.IsNullOrWhiteSpace(SmtpHost) || SmtpPort is < 1 or > 65535 || string.IsNullOrWhiteSpace(SmtpUsername) || string.IsNullOrEmpty(SmtpPassword)
            ? "Thiếu cấu hình Email:SmtpHost / SmtpPort / SmtpUsername / SmtpPassword."
            : null;
    }

    private static bool IsEmail(string value) => MailAddress.TryCreate(value, out _);
}
