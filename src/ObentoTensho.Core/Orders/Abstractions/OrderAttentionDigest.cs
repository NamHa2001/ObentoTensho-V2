using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Core.Orders.Abstractions;

// Nội dung thư "cần xử lý" gửi quản lý: toàn bộ đơn đang cần để ý, gộp một thư (BR-NTF-09)
public sealed record OrderAttentionDigest(
    IReadOnlyList<Order> PendingOverdue,
    IReadOnlyList<Order> NotPickedUp,
    IReadOnlyList<Order> RefundPending);
