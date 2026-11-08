using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Services;

// BR-LOY-09/20: mỗi năm một voucher sinh nhật cho khách đã xác thực email (ngày VN); chỉ mục UNIQUE giữ idempotent khi chạy lại.
// Sinh 29/2 thì năm thường nhận vào 28/2. Hạn dùng theo voucher.redeemExpiryDays.
public sealed class BirthdayVoucherService(
    IVoucherRepository vouchers,
    ILoyaltySettings loyaltySettings,
    IVoucherSettings settings,
    IVoucherEmailOutbox emails,
    IDisposableEmailDomains disposableEmails,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int BatchSize = 500;
    private const string Prefix = "SN";

    public async Task<int> IssueDueAsync(CancellationToken cancellationToken)
    {
        if (loyaltySettings.BirthdayVoucherValue <= 0)
        {
            return 0;
        }

        var today = clock.TodayLocal;
        var includeLeapDay = today is { Month: 2, Day: 28 } && !DateTime.IsLeapYear(today.Year);
        var issued = 0;

        // Đọc hết theo lô (khóa Id tăng dần): tài khoản email dùng một lần không bao giờ nhận voucher nên lần nào cũng
        // còn "đến hạn" — chỉ đọc một lô thì chúng có thể chiếm chỗ và khách thật phía sau không bao giờ tới lượt
        Guid? after = null;
        while (true)
        {
            var batch = await vouchers.ListBirthdayDueAsync(today.Month, today.Day, includeLeapDay, today.Year, after, BatchSize, cancellationToken);

            // BR-AUTH-16: email dùng một lần không nhận ưu đãi
            foreach (var user in batch.Where(u => !disposableEmails.IsDisposable(u.Email)))
            {
                issued += await IssueAsync(user, today, cancellationToken) ? 1 : 0;
            }

            if (batch.Count < BatchSize)
            {
                return issued;
            }

            after = batch[^1].Id;
        }
    }

    private async Task<bool> IssueAsync(User user, DateOnly today, CancellationToken cancellationToken)
    {
        try
        {
            return await unitOfWork.InTransactionAsync(async ct =>
            {
                var voucher = await NewVoucherAsync(user.Id, today, ct);
                vouchers.Add(voucher);
                emails.EnqueueBirthday(user, voucher);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Lượt chạy khác vừa cấp cho khách này trong năm nay
            return false;
        }
    }

    private async Task<Voucher> NewVoucherAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        string code;
        do
        {
            code = VoucherCodes.New(Prefix);
        }
        while ((await vouchers.ExistingCodesAsync([code], cancellationToken)).Count > 0);

        var now = clock.UtcNow;
        return new Voucher
        {
            Code = code,
            DiscountType = DiscountType.Fixed,
            DiscountValue = loyaltySettings.BirthdayVoucherValue,
            MaxUsageTotal = 1,
            MaxUsagePerUser = 1,
            OwnerUserId = userId,
            RequireLogin = true,
            Source = VoucherSource.Birthday,
            BirthdayYear = today.Year,
            StartUtc = now,
            EndUtc = clock.ToUtc(today.AddDays(settings.RedeemExpiryDays + 1), TimeOnly.MinValue),
            CreatedAtUtc = now,
        };
    }
}
