namespace ObentoTensho.Contracts.Enums;

// BR-ORD-09: chỉ chuyển Unpaid → Paid, đúng một lần
public enum PaymentStatus : byte
{
    Unpaid = 0,
    Paid = 1
}
