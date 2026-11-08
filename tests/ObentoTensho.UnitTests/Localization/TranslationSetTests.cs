using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Localization.Services;

namespace ObentoTensho.UnitTests.Localization;

// BR-I18N-02/03/07: chỉ bản đã duyệt, còn khớp bản Việt; thiếu trường thì lấy tiếng Việt và bật cờ
public sealed class TranslationSetTests
{
    private static readonly SourceText Source = new("Tin mới", "Tóm tắt", "<p>Nội dung</p>");

    private static TranslationSet Set(string? title, string? summary, string? body, TranslationStatus status, string? hash = null) =>
        new("en", new Dictionary<int, ContentTranslation>
        {
            [1] = new NewsTranslation
            {
                EntityId = 1, Lang = "en", Title = title, Summary = summary, BodyHtml = body,
                SourceHash = hash ?? Source.Hash, ReviewStatus = status, UpdatedAtUtc = DateTime.UtcNow,
            },
        });

    [Fact]
    public void Ban_duyet_day_du_thi_hien_ban_dich()
    {
        var shown = Set("News", "Summary", "<p>Body</p>", TranslationStatus.Approved).Apply(1, Source);

        Assert.Equal(new LocalizedText(new SourceText("News", "Summary", "<p>Body</p>"), "en", false), shown);
    }

    [Fact]
    public void Ban_nhap_ban_loi_thoi_hoac_khong_co_thi_hien_tieng_Viet_kem_co()
    {
        Assert.Equal(new LocalizedText(Source, "vi", true), Set("News", null, "<p>Body</p>", TranslationStatus.Draft).Apply(1, Source));
        Assert.Equal(new LocalizedText(Source, "vi", true), Set("News", null, "<p>Body</p>", TranslationStatus.Approved, hash: "cu").Apply(1, Source));
        Assert.Equal(new LocalizedText(Source, "vi", true), Set("News", null, null, TranslationStatus.Approved).Apply(2, Source));
    }

    [Fact]
    public void Thieu_mot_truong_thi_lay_truong_tieng_Viet_va_bat_co()
    {
        var shown = Set("News", null, "<p>Body</p>", TranslationStatus.Approved).Apply(1, Source);

        Assert.Equal(("News", "Tóm tắt", "en", true), (shown.Text.Title, shown.Text.Summary, shown.ContentLang, shown.IsFallback));
    }

    [Fact]
    public void Tieng_Viet_khong_bat_co()
    {
        Assert.Equal(new LocalizedText(Source, "vi", false), TranslationSet.Source.Apply(1, Source));
    }

    [Fact]
    public void Doi_bat_ky_truong_ban_Viet_nao_cung_doi_hash()
    {
        Assert.NotEqual(Source.Hash, (Source with { Summary = "Tóm tắt khác" }).Hash, StringComparer.Ordinal);
        Assert.NotEqual(Source.Hash, (Source with { BodyHtml = "<p>Khác</p>" }).Hash, StringComparer.Ordinal);
        Assert.NotEqual(new SourceText("a", "b").Hash, new SourceText("ab").Hash, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Sale", true)]
    [InlineData("セール 10%", true)]
    [InlineData("Line\nbreak", true)]
    [InlineData("Sale\u202Eevil", false)]
    [InlineData("Sale\u2066x", false)]
    [InlineData("Sale\u200Fx", false)]
    [InlineData("Sale\u0007", false)]
    public void Chan_ky_tu_dieu_khien_va_dao_chieu(string text, bool safe)
    {
        Assert.Equal(safe, TranslationTextRules.IsSafeText(text));
    }

    [Theory]
    [InlineData(null, "vi")]
    [InlineData("en", "en")]
    [InlineData(" EN ", "en")]
    [InlineData("ja", "vi")]
    [InlineData("fr", "vi")]
    [InlineData("../en", "vi")]
    public void Chi_chon_ngon_ngu_dang_bat(string? requested, string expected)
    {
        var languages = new ContentLanguages(new FakeSettings("vi,en"));

        Assert.Equal(expected, languages.Resolve(requested));
    }

    private sealed class FakeSettings(string locales) : ObentoTensho.Core.Settings.Abstractions.ISettingsReader
    {
        public string Get(string key) => locales;

        public int GetInt(string key) => 0;

        public bool GetBool(string key) => false;

        public IReadOnlyDictionary<string, string> Public() => new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
