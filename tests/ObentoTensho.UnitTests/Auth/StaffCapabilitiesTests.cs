using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.UnitTests.Auth;

// SEC-12: ma trận quyền chép lại độc lập từ docs/srs/01 §3 — sửa mã mà quên sửa SRS (hoặc ngược lại) thì test đỏ
public sealed class StaffCapabilitiesTests
{
    // Thứ tự cột: SA, MG, RC, KT, CS, CE
    public static TheoryData<StaffCapability, string> Matrix => new()
    {
        { StaffCapability.Dashboard, "✓✓✓---" },
        { StaffCapability.Reservations, "✓✓✓---" },
        { StaffCapability.TablesView, "✓✓✓---" },
        { StaffCapability.TablesManage, "✓✓----" },
        { StaffCapability.Shifts, "✓✓----" },
        { StaffCapability.OrdersView, "✓✓-✓✓-" },
        { StaffCapability.OrdersKitchen, "✓✓-✓--" },
        { StaffCapability.OrdersCashier, "✓✓--✓-" },
        { StaffCapability.OrdersCancel, "✓✓-✓✓-" },
        { StaffCapability.Menu, "✓✓----" },
        { StaffCapability.Customers, "✓✓----" },
        { StaffCapability.Feedback, "✓✓----" },
        { StaffCapability.Games, "✓✓----" },
        { StaffCapability.Content, "✓✓---✓" },
        { StaffCapability.Reports, "✓✓----" },
        { StaffCapability.PaymentSettings, "✓-----" },
        { StaffCapability.CustomerExport, "✓✓----" },
        { StaffCapability.RegularHours, "✓-----" },
        { StaffCapability.SpecialHours, "✓✓----" },
        { StaffCapability.StaffAndAudit, "✓-----" },
    };

    private static readonly StaffRole[] Columns =
        [StaffRole.SuperAdmin, StaffRole.Manager, StaffRole.Reception, StaffRole.Kitchen, StaffRole.Cashier, StaffRole.ContentEditor];

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Ma_tran_khop_SRS(StaffCapability capability, string row)
    {
        for (var i = 0; i < Columns.Length; i++)
        {
            Assert.True(
                StaffCapabilities.Has(Columns[i], capability) == (row[i] == '✓'),
                $"{capability} × {Columns[i]}: SRS ghi '{row[i]}'");
        }
    }

    [Fact]
    public void Moi_quyen_deu_co_trong_ma_tran_va_SuperAdmin_co_tat_ca()
    {
        Assert.Equal(Enum.GetValues<StaffCapability>().Length, Matrix.Count);
        Assert.All(Enum.GetValues<StaffCapability>(), c => Assert.True(StaffCapabilities.Has(StaffRole.SuperAdmin, c)));
    }
}
