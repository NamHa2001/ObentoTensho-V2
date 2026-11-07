namespace ObentoTensho.Contracts.Enums;

// Nguồn voucher (docs/srs/08 §6): quản lý tạo, đổi điểm, sinh nhật, thưởng game (Phase 2)
public enum VoucherSource : byte
{
    Admin = 0,
    Redeem = 1,
    Birthday = 2,
    Game = 3
}
