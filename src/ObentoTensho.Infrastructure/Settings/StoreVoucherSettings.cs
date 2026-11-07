using Microsoft.Extensions.Options;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Infrastructure.Vouchers;
using static ObentoTensho.Core.Settings.Domain.SettingKeys;

namespace ObentoTensho.Infrastructure.Settings;

internal sealed class StoreVoucherSettings(ISettingsReader s, IOptions<VoucherOptions> options) : IVoucherSettings
{
    public int RedeemExpiryDays => s.GetInt(VoucherRedeemExpiryDays);

    public int ApprovalPercent => s.GetInt(VoucherApprovalPercent);

    public decimal ApprovalAmount => s.GetInt(VoucherApprovalAmount);

    public int BulkApprovalQuantity => options.Value.BulkApprovalQuantity;
}
