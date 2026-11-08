using ObentoTensho.Api.Http;

namespace ObentoTensho.IntegrationTests.Http;

public sealed class TurnstileOutageTests
{
    private static readonly DateTime Start = new(2026, 11, 8, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Chi_bao_mot_lan_khi_loi_qua_10_phut_va_dat_lai_khi_hoat_dong()
    {
        var outage = new TurnstileOutage();
        Assert.Null(outage.Down(Start));
        Assert.Null(outage.Down(Start + TurnstileOutage.AlertAfter - TimeSpan.FromSeconds(1)));
        Assert.Equal(Start, outage.Down(Start + TurnstileOutage.AlertAfter));
        Assert.Null(outage.Down(Start + TimeSpan.FromHours(1)));

        outage.Up();
        var next = Start + TimeSpan.FromHours(2);
        Assert.Null(outage.Down(next));
        Assert.Equal(next, outage.Down(next + TurnstileOutage.AlertAfter));
    }
}
