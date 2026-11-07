namespace ObentoTensho.Contracts.Enums;

// FR-ORD-03: tiền mặt khi nhận món hoặc chuyển khoản (thu ngân đối chiếu)
public enum PaymentMethod : byte
{
    Cash = 0,
    BankTransfer = 1
}
