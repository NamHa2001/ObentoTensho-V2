using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.UnitTests.Reservations;

public sealed class ReservationDomainTests
{
    private const ReservationStatus P = ReservationStatus.Pending;
    private const ReservationStatus C = ReservationStatus.Confirmed;
    private const ReservationStatus I = ReservationStatus.CheckedIn;
    private const ReservationStatus D = ReservationStatus.Completed;
    private const ReservationStatus X = ReservationStatus.Cancelled;
    private const ReservationStatus N = ReservationStatus.NoShow;

    // Bảng trạng thái docs/srs/11 §6
    [Theory]
    [InlineData(P, C, ReservationActor.Staff, true)]
    [InlineData(P, C, ReservationActor.Guest, false)]
    [InlineData(P, X, ReservationActor.Guest, true)]
    [InlineData(P, X, ReservationActor.System, true)]
    [InlineData(C, X, ReservationActor.Guest, true)]
    [InlineData(C, X, ReservationActor.System, false)]
    [InlineData(C, I, ReservationActor.Staff, true)]
    [InlineData(C, N, ReservationActor.System, true)]
    [InlineData(C, N, ReservationActor.Guest, false)]
    [InlineData(I, D, ReservationActor.Staff, true)]
    [InlineData(I, X, ReservationActor.Guest, false)]
    [InlineData(D, C, ReservationActor.Staff, false)]
    [InlineData(X, C, ReservationActor.Staff, false)]
    [InlineData(N, I, ReservationActor.Staff, true)]
    [InlineData(N, C, ReservationActor.Staff, false)]
    [InlineData(P, I, ReservationActor.Staff, false)]
    public void May_trang_thai_dung_bang_SRS(ReservationStatus from, ReservationStatus to, ReservationActor actor, bool allowed)
    {
        Assert.Equal(allowed, ReservationStateMachine.CanTransition(from, to, actor));
    }

    [Fact]
    public void Nhan_vien_huy_tu_choi_hoac_khoi_phuc_no_show_phai_co_ly_do()
    {
        Assert.True(ReservationStateMachine.RequiresReason(P, X, ReservationActor.Staff));
        Assert.True(ReservationStateMachine.RequiresReason(N, I, ReservationActor.Staff));
        Assert.False(ReservationStateMachine.RequiresReason(P, X, ReservationActor.Guest));
        Assert.False(ReservationStateMachine.RequiresReason(C, I, ReservationActor.Staff));
    }

    [Fact]
    public void Ma_dat_ban_ngau_nhien_dung_dinh_dang_khong_trung()
    {
        var codes = Enumerable.Range(0, 2000).Select(_ => ReservationCodes.New()).ToArray();

        Assert.All(codes, c => Assert.Matches("^OBT-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$", c));
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("OBT-8K4Q-7ZM2", "OBT-8K4Q-7ZM2")]
    [InlineData("obt-8k4q-7zm2", "OBT-8K4Q-7ZM2")]
    [InlineData("OBT8K4Q7ZM2", "OBT-8K4Q-7ZM2")]
    [InlineData(" 8k4q 7zm2 ", "OBT-8K4Q-7ZM2")]
    [InlineData("OBT-8K4Q-7ZMO", "OBT-8K4Q-7ZM0")]
    [InlineData("OBT-8K4Q", "")]
    [InlineData(null, "")]
    public void Chuan_hoa_ma_khach_go(string? input, string expected)
    {
        Assert.Equal(expected, ReservationCodes.Normalize(input));
    }

    [Fact]
    public void Che_lien_he_cong_khai()
    {
        Assert.Equal("091****678", PiiMask.Phone("+84912345678"));
        Assert.Equal("n***@gmail.com", PiiMask.Email("nguyen.an@gmail.com"));
        Assert.Equal("5678", PiiMask.Last4("+84912345678"));
    }
}
