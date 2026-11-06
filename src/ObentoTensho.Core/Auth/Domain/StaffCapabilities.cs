using System.Collections.Frozen;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Domain;

// Ma trận quyền docs/srs/01 §3 (SEC-12). Sửa ở đây phải sửa cả SRS và test ma trận.
public static class StaffCapabilities
{
    private const StaffRole SA = StaffRole.SuperAdmin;
    private const StaffRole MG = StaffRole.Manager;
    private const StaffRole RC = StaffRole.Reception;
    private const StaffRole KT = StaffRole.Kitchen;
    private const StaffRole CS = StaffRole.Cashier;
    private const StaffRole CE = StaffRole.ContentEditor;

    private static readonly FrozenDictionary<StaffCapability, StaffRole[]> Matrix = new Dictionary<StaffCapability, StaffRole[]>
    {
        // Reception chỉ thấy KPI đặt bàn/bàn — server không trả doanh thu (BR-RPT-32)
        [StaffCapability.Dashboard] = [SA, MG, RC],
        [StaffCapability.Reservations] = [SA, MG, RC],
        [StaffCapability.TablesView] = [SA, MG, RC],
        [StaffCapability.TablesManage] = [SA, MG],
        [StaffCapability.Shifts] = [SA, MG],
        [StaffCapability.OrdersView] = [SA, MG, KT, CS],
        [StaffCapability.OrdersKitchen] = [SA, MG, KT],
        [StaffCapability.OrdersCashier] = [SA, MG, CS],
        [StaffCapability.OrdersCancel] = [SA, MG, KT, CS],
        [StaffCapability.Menu] = [SA, MG],
        [StaffCapability.Customers] = [SA, MG],
        [StaffCapability.Feedback] = [SA, MG],
        [StaffCapability.Games] = [SA, MG],
        [StaffCapability.Content] = [SA, MG, CE],
        [StaffCapability.Reports] = [SA, MG],
        [StaffCapability.PaymentSettings] = [SA],
        [StaffCapability.CustomerExport] = [SA, MG],
        [StaffCapability.RegularHours] = [SA],
        [StaffCapability.SpecialHours] = [SA, MG],
        [StaffCapability.StaffAndAudit] = [SA],
    }.ToFrozenDictionary();

    public static IReadOnlyList<StaffRole> RolesFor(StaffCapability capability) =>
        Matrix.TryGetValue(capability, out var roles) ? roles : [];

    public static bool Has(StaffRole role, StaffCapability capability) => RolesFor(capability).Contains(role);

    public static IReadOnlyList<StaffCapability> For(StaffRole role) =>
        Enum.GetValues<StaffCapability>().Where(p => Has(role, p)).ToArray();
}
