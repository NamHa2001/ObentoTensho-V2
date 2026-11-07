namespace ObentoTensho.Contracts.Enums;

// docs/srs/13 §6: loại dòng sổ cái điểm; giá trị số khớp cột TransactionType (tinyint)
public enum LoyaltyTransactionType : byte
{
    ReservationEarn = 0,
    OrderEarn = 1,
    Redeem = 2,
    Expired = 3,
    AdminAdjust = 4,
    Revoke = 5,
    DebtRepaid = 6
}
