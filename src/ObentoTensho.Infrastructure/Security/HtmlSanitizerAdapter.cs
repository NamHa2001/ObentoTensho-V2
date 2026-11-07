using Ganss.Xss;
using IHtmlSanitizer = ObentoTensho.Core.Common.IHtmlSanitizer;

namespace ObentoTensho.Infrastructure.Security;

// SEC-08: chỉ giữ thẻ định dạng văn bản cơ bản (bài viết thêm ảnh); bỏ script, style, iframe, thuộc tính sự kiện và liên kết javascript:
internal sealed class HtmlSanitizerAdapter : IHtmlSanitizer
{
    private static readonly string[] AllowedTags =
        ["p", "br", "strong", "b", "em", "i", "u", "s", "ul", "ol", "li", "h3", "h4", "blockquote", "a", "span"];

    private static readonly string[] RichTags = [.. AllowedTags, "h2", "img", "figure", "figcaption", "hr"];

    private readonly HtmlSanitizer _sanitizer = Create(AllowedTags, ["href", "title"], ["href"]);
    private readonly HtmlSanitizer _richSanitizer = Create(RichTags, ["href", "title", "src", "alt", "width", "height"], ["href", "src"]);

    public string Sanitize(string html) => _sanitizer.Sanitize(html ?? string.Empty).Trim();

    public string SanitizeRich(string html) => _richSanitizer.Sanitize(html ?? string.Empty).Trim();

    private static HtmlSanitizer Create(string[] tags, string[] attributes, string[] uriAttributes)
    {
        var options = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(attributes, StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(["http", "https", "mailto"], StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(uriAttributes, StringComparer.OrdinalIgnoreCase),
        };
        var sanitizer = new HtmlSanitizer(options);

        // Liên kết mở tab mới không cho trang đích truy cập window.opener
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement { LocalName: "a" } link)
            {
                link.SetAttribute("rel", "noopener noreferrer nofollow");
            }
        };
        return sanitizer;
    }
}
