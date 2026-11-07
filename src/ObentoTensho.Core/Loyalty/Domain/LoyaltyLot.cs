namespace ObentoTensho.Core.Loyalty.Domain;

// BR-LOY-06: phần còn lại của mỗi lần cộng điểm, tiêu/hết hạn theo thứ tự hạn sớm nhất trước (FIFO).
// Đối soát: Users.CurrentPoints = Σ RemainingPoints = Σ Points của sổ cái (BR-LOY-04).
public sealed class LoyaltyLot
{
    public long LedgerId { get; private set; }

    public required LoyaltyEntry Entry { get; init; }

    public required Guid UserId { get; init; }

    public required int RemainingPoints { get; set; }

    public required DateTime ExpiresAtUtc { get; init; }

    // BR-LOY-12: đã báo "điểm sắp hết hạn" cho lô này
    public DateTime? ExpiryNoticeSentAtUtc { get; set; }
}
