using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.UnitTests.Security;

// BR-AUTH-16
public sealed class DisposableEmailDomainsTests
{
    private readonly EmbeddedDisposableEmailDomains _domains = new();

    [Theory]
    [InlineData("an@mailinator.com", true)]
    [InlineData("an@MAILINATOR.COM", true)]
    [InlineData("an@abc.mailinator.com", true)]
    [InlineData("an@yopmail.com.", true)]
    [InlineData("an@gmail.com", false)]
    [InlineData("an@notmailinator.com", false)]
    [InlineData("khong-co-a-cong", false)]
    public void Nhan_ra_ten_mien_dung_mot_lan(string email, bool expected)
    {
        Assert.Equal(expected, _domains.IsDisposable(email));
    }

    [Fact]
    public void Danh_sach_nap_duoc_tu_tep()
    {
        Assert.True(EmbeddedDisposableEmailDomains.Count >= 50);
    }
}
