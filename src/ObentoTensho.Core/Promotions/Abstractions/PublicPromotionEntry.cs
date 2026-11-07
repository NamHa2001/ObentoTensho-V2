using ObentoTensho.Core.Promotions.Domain;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Promotions.Abstractions;

// Khuyến mãi chưa kết thúc kèm voucher công khai; hiệu lực theo ngày lọc lúc đọc nên cache không làm sai ngày
public sealed record PublicPromotionEntry(Promotion Promotion, IReadOnlyList<Voucher> Vouchers);
