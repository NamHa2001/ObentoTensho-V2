namespace ObentoTensho.Core.Notifications.Domain;

// Tệp đính kèm dạng văn bản (vd lịch .ics) — nội dung nhỏ, lưu được trong hộp thư đi
public sealed record EmailAttachment(string FileName, string ContentType, string Content);
