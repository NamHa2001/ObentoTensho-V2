using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.UnitTests.Vouchers;

// docs/srs/14 §8: bảng hợp lệ, mức giảm, mã không phân biệt hoa thường, voucher cần duyệt
public sealed class VoucherRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 5, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Member = Guid.NewGuid();

    private static Voucher Valid() => new()
    {
        Code = "TET2027",
        DiscountType = DiscountType.Percent,
        DiscountValue = 10,
        MaxDiscountAmount = 20_000,
        MinOrderValue = 50_000,
        MaxUsageTotal = 100,
        MaxUsagePerUser = 1,
        StartUtc = Now.AddDays(-1),
        EndUtc = Now.AddDays(1),
        CreatedAtUtc = Now,
    };

    private static VoucherRules.Context Ctx(decimal subTotal = 100_000, Guid? user = null, MemberTier? tier = null, int used = 0) =>
        new(Now, subTotal, user, tier, used);

    [Fact]
    public void Hop_le_khi_du_moi_dieu_kien() => Assert.Null(VoucherRules.Check(Valid(), Ctx()));

    public static TheoryData<string, string> Violations() => new()
    {
        { "missing", ErrorCodes.VoucherInvalid },
        { "inactive", ErrorCodes.VoucherInvalid },
        { "pending", ErrorCodes.VoucherInvalid },
        { "not-started", ErrorCodes.VoucherInvalid },
        { "expired", ErrorCodes.VoucherInvalid },
        { "other-owner", ErrorCodes.VoucherInvalid },
        { "login", ErrorCodes.VoucherLoginRequired },
        { "min-order", ErrorCodes.VoucherMinOrder },
        { "tier", ErrorCodes.VoucherTierMismatch },
        { "total-used", ErrorCodes.VoucherExhausted },
        { "user-used", ErrorCodes.VoucherExhausted },
    };

    [Theory]
    [MemberData(nameof(Violations))]
    public void Moi_dieu_kien_vi_pham_bao_dung_ly_do(string violation, string error)
    {
        var context = Ctx();
        Voucher? voucher = Valid();
        switch (violation)
        {
            case "missing":
                voucher = null;
                break;
            case "inactive":
                voucher.IsActive = false;
                break;
            case "pending":
                voucher.ApprovalStatus = ApprovalStatus.Pending;
                break;
            case "not-started":
                voucher.StartUtc = Now.AddMinutes(1);
                break;
            case "expired":
                voucher.EndUtc = Now;
                break;
            case "other-owner":
                voucher = new Voucher { Code = "DIEM-X", DiscountValue = 1, OwnerUserId = Guid.NewGuid(), CreatedAtUtc = Now };
                context = Ctx(user: Member);
                break;
            case "login":
                voucher.RequireLogin = true;
                break;
            case "min-order":
                context = Ctx(subTotal: 49_999);
                break;
            case "tier":
                voucher.ApplicableTier = MemberTier.Gold;
                context = Ctx(user: Member, tier: MemberTier.Silver);
                break;
            case "total-used":
                voucher.MaxUsageTotal = 0;
                break;
            case "user-used":
                context = Ctx(user: Member, used: 1);
                break;
        }

        Assert.Equal(error, VoucherRules.Check(voucher, context));
    }

    [Theory]
    [InlineData(DiscountType.Percent, 10, null, 123_456, 12_345)]
    [InlineData(DiscountType.Percent, 10, 20_000, 500_000, 20_000)]
    [InlineData(DiscountType.Fixed, 30_000, null, 100_000, 30_000)]
    [InlineData(DiscountType.Fixed, 30_000, null, 20_000, 20_000)]
    [InlineData(DiscountType.Percent, 100, null, 80_000, 80_000)]
    public void Muc_giam_lam_tron_xuong_va_khong_vuot_tam_tinh(DiscountType type, int value, int? cap, int subTotal, int expected)
    {
        var voucher = new Voucher { Code = "X", DiscountType = type, DiscountValue = value, MaxDiscountAmount = cap, CreatedAtUtc = Now };

        Assert.Equal(expected, VoucherRules.Discount(voucher, subTotal));
    }

    [Theory]
    [InlineData(" abcd-1234 ", "ABCD-1234")]
    [InlineData("Tet2027", "TET2027")]
    public void Ma_khong_phan_biet_hoa_thuong(string input, string normalized)
    {
        Assert.Equal(normalized, VoucherCodes.Normalize(input));
        Assert.True(VoucherCodes.IsValid(normalized));
    }

    [Fact]
    public void Ma_sinh_tu_dong_du_dai_va_khong_trung()
    {
        var codes = Enumerable.Range(0, 2000).Select(_ => VoucherCodes.New("TET")).ToArray();

        Assert.All(codes, c => Assert.Matches("^TET-[0-9A-HJKMNP-TV-Z]{10}$", c));
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(DiscountType.Percent, 49, 100, false)]
    [InlineData(DiscountType.Percent, 50, 100, true)]
    [InlineData(DiscountType.Fixed, 199_999, 100, false)]
    [InlineData(DiscountType.Fixed, 200_000, 100, true)]
    [InlineData(DiscountType.Percent, 5, null, true)]
    public void Voucher_gia_tri_cao_hoac_khong_gioi_han_luot_can_duyet(DiscountType type, int value, int? maxTotal, bool needs) =>
        Assert.Equal(needs, VoucherRules.NeedsApproval(type, value, maxTotal, new Settings()));

    // Mã giảm ship: chỉ dùng khi đơn có phí ship; giảm trên phí ship, không vượt phí ship
    [Fact]
    public void Ma_giam_ship_chi_dung_khi_co_phi_ship_va_khong_vuot_phi_ship()
    {
        var freeShip = Valid();
        freeShip.AppliesTo = VoucherTarget.Shipping;
        freeShip.DiscountValue = 100;
        freeShip.MaxDiscountAmount = null;

        Assert.Equal(ErrorCodes.VoucherShippingOnly, VoucherRules.Check(freeShip, Ctx()));
        Assert.Null(VoucherRules.Check(freeShip, Ctx() with { ShippingFee = 45_000 }));
        Assert.Equal(45_000, VoucherRules.Discount(freeShip, subTotal: 100_000, shippingFee: 45_000));

        freeShip.DiscountType = DiscountType.Fixed;
        freeShip.DiscountValue = 20_000;
        Assert.Equal(20_000, VoucherRules.Discount(freeShip, subTotal: 100_000, shippingFee: 45_000));
        Assert.Equal(15_000, VoucherRules.Discount(freeShip, subTotal: 100_000, shippingFee: 15_000));
    }

    [Theory]
    [InlineData(DiscountType.Percent, 100, 100, false)]
    [InlineData(DiscountType.Fixed, 500_000, 100, false)]
    [InlineData(DiscountType.Percent, 100, null, true)]
    public void Ma_giam_ship_chi_can_duyet_khi_khong_gioi_han_luot(DiscountType type, int value, int? maxTotal, bool needs) =>
        Assert.Equal(needs, VoucherRules.NeedsApproval(type, value, maxTotal, new Settings(), VoucherTarget.Shipping));

    private sealed class Settings : IVoucherSettings
    {
        public int RedeemExpiryDays => 30;

        public int ApprovalPercent => 50;

        public decimal ApprovalAmount => 200_000m;

        public int BulkApprovalQuantity => 500;
    }
}
