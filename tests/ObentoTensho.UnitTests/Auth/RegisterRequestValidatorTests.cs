using Microsoft.Extensions.Time.Testing;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.Infrastructure.Time;

namespace ObentoTensho.UnitTests.Auth;

public sealed class RegisterRequestValidatorTests
{
    private static readonly RegisterRequest Valid = new(
        "Nguyễn Văn An", "an.nguyen@example.com", "0912345678", "Bento-thom-ngon-2026", new DateOnly(1995, 3, 15), "vi");

    private readonly RegisterRequestValidator _validator = new(
        new EmbeddedCommonPasswordList(),
        new SystemClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 6, 3, 0, 0, TimeSpan.Zero))));

    private string[] ErrorsFor(RegisterRequest request, string property) =>
        _validator.Validate(request).Errors
            .Where(e => string.Equals(e.PropertyName, property, StringComparison.Ordinal))
            .Select(e => e.ErrorCode)
            .ToArray();

    [Fact]
    public void Du_lieu_hop_le_duoc_chap_nhan()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }

    [Theory]
    [InlineData("Ngan-2026", FieldErrorCodes.TooShort)]
    [InlineData("qwertyuiop", FieldErrorCodes.PasswordTooCommon)]
    [InlineData("QWERTYUIOP", FieldErrorCodes.PasswordTooCommon)]
    [InlineData("an.nguyen-2026", FieldErrorCodes.PasswordContainsEmail)]
    public void Mat_khau_vi_pham_BR_AUTH_01(string password, string expected)
    {
        Assert.Contains(expected, ErrorsFor(Valid with { Password = password }, nameof(RegisterRequest.Password)), StringComparer.Ordinal);
    }

    [Fact]
    public void Mat_khau_qua_dai_bi_chan_de_tranh_ton_CPU()
    {
        Assert.Contains(FieldErrorCodes.TooLong, ErrorsFor(Valid with { Password = new string('x', 129) }, nameof(RegisterRequest.Password)), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("khong-co-a-cong")]
    [InlineData("a@b")]
    [InlineData("hai@@example.com")]
    [InlineData("có-dấu@example.com")]
    [InlineData("a b@example.com")]
    public void Email_sai_dinh_dang(string email)
    {
        Assert.Contains(FieldErrorCodes.InvalidFormat, ErrorsFor(Valid with { Email = email }, nameof(RegisterRequest.Email)), StringComparer.Ordinal);
    }

    [Fact]
    public void Ngay_sinh_trong_tuong_lai_theo_gio_VN_bi_tu_choi()
    {
        Assert.Contains(FieldErrorCodes.OutOfRange, ErrorsFor(Valid with { DateOfBirth = new DateOnly(2026, 11, 7) }, nameof(RegisterRequest.DateOfBirth)), StringComparer.Ordinal);
        Assert.Empty(ErrorsFor(Valid with { DateOfBirth = new DateOnly(2026, 11, 6) }, nameof(RegisterRequest.DateOfBirth)));
    }

    [Fact]
    public void Ngon_ngu_ngoai_vi_en_ja_bi_tu_choi()
    {
        Assert.Contains(FieldErrorCodes.OutOfRange, ErrorsFor(Valid with { PreferredLang = "fr" }, nameof(RegisterRequest.PreferredLang)), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("+1 415 555 0100")]
    public void So_dien_thoai_khong_bat_buoc_nhung_nhap_sai_thi_bi_tu_choi(string phone)
    {
        Assert.Empty(ErrorsFor(Valid with { PhoneNumber = null }, nameof(RegisterRequest.PhoneNumber)));
        Assert.Contains(FieldErrorCodes.InvalidFormat, ErrorsFor(Valid with { PhoneNumber = phone }, nameof(RegisterRequest.PhoneNumber)), StringComparer.Ordinal);
    }

    [Fact]
    public void Truong_bat_buoc_bi_bo_trong()
    {
        var empty = new RegisterRequest(" ", " ", "", "", null, null);

        Assert.Contains(FieldErrorCodes.Required, ErrorsFor(empty, nameof(RegisterRequest.FullName)), StringComparer.Ordinal);
        Assert.Contains(FieldErrorCodes.Required, ErrorsFor(empty, nameof(RegisterRequest.Email)), StringComparer.Ordinal);
        Assert.Empty(ErrorsFor(empty, nameof(RegisterRequest.PhoneNumber)));
        Assert.Contains(FieldErrorCodes.Required, ErrorsFor(empty, nameof(RegisterRequest.Password)), StringComparer.Ordinal);
    }
}
