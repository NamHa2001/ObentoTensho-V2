using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Abstractions;

// docs/srs/20 #17: đổi điểm thành công — gửi mã voucher
public interface IVoucherEmailOutbox
{
    void EnqueueRedeemed(User user, Voucher voucher, int points);
}
