using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Loyalty.Services;

// BR-LOY-04, TST-37: đối soát 03:45 giờ VN — số dư = tổng sổ cái = tổng điểm còn lại của lô. Lệch thì không tự sửa
// (điểm là tài sản của khách, sửa phải có người duyệt), chỉ báo SuperAdmin kèm danh sách khách lệch
public sealed class LoyaltyReconcileJob(
    ILoyaltyRepository loyalty,
    IStaffRepository staff,
    IAdminEmailOutbox adminEmails,
    IUnitOfWork unitOfWork,
    IClock clock) : IDailyJob
{
    private const int MaxListed = 50;

    public string Name => "loyalty.reconcile";

    public TimeOnly RunAtLocal => new(3, 45);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var mismatched = await loyalty.FindBalanceMismatchesAsync(MaxListed, cancellationToken);
        if (mismatched.Count == 0)
        {
            return 0;
        }

        var message = $"Đối soát điểm phát hiện {mismatched.Count}{(mismatched.Count == MaxListed ? "+" : string.Empty)} khách có số dư lệch sổ cái: "
            + string.Join(", ", mismatched.Select(id => id.ToString("D")))
            + ". Không tự sửa; hãy kiểm tra lịch sử điểm của các khách này.";
        var key = $"loyalty-reconcile:{clock.TodayLocal:yyyyMMdd}";
        foreach (var email in await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin], cancellationToken))
        {
            adminEmails.EnqueueAlert(email, message, key);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Hôm nay đã báo (cùng khóa thư) — không báo lại
            return 0;
        }

        return mismatched.Count;
    }
}
