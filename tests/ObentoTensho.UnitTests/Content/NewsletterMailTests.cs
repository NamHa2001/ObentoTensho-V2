using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Infrastructure.Email;

namespace ObentoTensho.UnitTests.Content;

// BR-CNT-24, BR-CNT-26: thư bản tin có header hủy một chạm, bản text thuần, chỉ nội dung đã làm sạch mới không escape
public sealed class NewsletterMailTests
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
    {
        ["subject"] = "<b>Tin</b>",
        [EmailRequest.TrustedHtmlValue] = "<p>Tin mới</p>",
        ["contentText"] = "Tin mới",
        ["unsubscribeLink"] = "https://www.obento.test/newsletter/unsubscribe#token=a\"><script>",
    };

    [Theory]
    [InlineData("<p>Một &amp; hai</p><ul><li>Ba</li></ul>", "Một & hai\nBa")]
    [InlineData("<h2>Tin</h2>Dòng<br>Dòng 2", "Tin\nDòng\nDòng 2")]
    public void Ban_text_bo_the_html(string html, string expected) =>
        Assert.Equal(expected, NewsletterText.FromHtml(html));

    [Fact]
    public void Chi_noi_dung_ban_tin_khong_bi_escape()
    {
        var message = new EmbeddedEmailTemplateRenderer().Render(new EmailRequest(EmailKind.Newsletter, "an@example.com", "vi", Values, "k", null));

        Assert.Contains("<p>Tin mới</p>", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Tin</b>", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Header_rieng_cua_thu_vao_ban_MIME()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["List-Unsubscribe"] = "<https://www.obento.test/api/v1/newsletter/one-click?token=abc>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        var message = new EmbeddedEmailTemplateRenderer().Render(new EmailRequest(EmailKind.Newsletter, "an@example.com", "vi", Values, "k", null, Headers: headers));

        var mime = MimeMessageFactory.Create(message, new EmailOptions { FromName = "Obento Tensho", FromAddress = "no-reply@obento.test" });

        Assert.Equal("List-Unsubscribe=One-Click", mime.Headers["List-Unsubscribe-Post"]);
        Assert.Contains("one-click?token=abc", mime.Headers["List-Unsubscribe"], StringComparison.Ordinal);
    }
}
