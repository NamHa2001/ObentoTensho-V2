namespace ObentoTensho.Contracts.Enums;

// docs/srs/23 BR-PLT-01: 4 nhóm thanh toán; chỉ Tại quán có tiền mặt, chuyển khoản. Giá trị số khớp cột PaymentGroup (tinyint)
public enum PaymentGroup : byte
{
    InStore = 0,
    Tensho = 1,
    Hima = 2,
    Xanh = 3
}
