using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Vouchers.Services;

// docs/srs/20 §4: voucher sinh nhật từ 08:00 giờ VN; chạy mỗi giờ để lượt lỗi được bù trong ngày (idempotent theo khách + năm)
public sealed class VoucherBirthdayJob(BirthdayVoucherService birthdays, IClock clock) : IRecurringJob
{
    private const int StartHour = 8;

    public string Name => "vouchers.birthday";

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public Task<int> RunAsync(CancellationToken cancellationToken) =>
        clock.ToLocal(clock.UtcNow).Hour < StartHour ? Task.FromResult(0) : birthdays.IssueDueAsync(cancellationToken);
}
