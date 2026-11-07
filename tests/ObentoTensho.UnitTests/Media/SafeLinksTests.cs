using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.UnitTests.Media;

// SEC-47: chống chuyển hướng mở qua liên kết banner; BR-CNT-35: video chỉ YouTube/TikTok
public sealed class SafeLinksTests
{
    private static readonly string[] Allowed = ["facebook.com"];

    [Theory]
    [InlineData("/menu", true)]
    [InlineData("/promotions/tet?x=1", true)]
    [InlineData("https://facebook.com/obento", true)]
    [InlineData("https://FACEBOOK.com/obento", true)]
    [InlineData("//evil.test", false)]
    [InlineData("/\\evil.test", false)]
    [InlineData("https://evil.test", false)]
    [InlineData("https://facebook.com@evil.test/", false)]
    [InlineData("http://facebook.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("menu", false)]
    public void Lien_ket_CTA(string link, bool allowed) =>
        Assert.Equal(allowed, SafeLinks.IsAllowedCta(link, Allowed));

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?t=5&v=dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.tiktok.com/@obento/video/7234567890123456789", "https://www.tiktok.com/embed/v2/7234567890123456789")]
    [InlineData("http://youtu.be/dQw4w9WgXcQ", null)]
    [InlineData("https://evil.test/watch?v=dQw4w9WgXcQ", null)]
    [InlineData("https://youtube.com.evil.test/watch?v=dQw4w9WgXcQ", null)]
    [InlineData("https://www.youtube.com/watch?v=<script>", null)]
    [InlineData("https://www.tiktok.com/@obento/photo/123456", null)]
    [InlineData(null, null)]
    public void Video_nhung_che_do_it_theo_doi(string? url, string? embed) =>
        Assert.Equal(embed, SafeLinks.VideoEmbedUrl(url));
}
