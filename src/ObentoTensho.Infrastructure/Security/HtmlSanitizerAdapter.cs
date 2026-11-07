using Ganss.Xss;
using IHtmlSanitizer = ObentoTensho.Core.Common.IHtmlSanitizer;

namespace ObentoTensho.Infrastructure.Security;

// SEC-08: chỉ giữ thẻ định dạng văn bản cơ bản; bỏ script, style, iframe, ảnh, thuộc tính sự kiện và liên kết javascript:
internal sealed class HtmlSanitizerAdapter : IHtmlSanitizer
{
    private static readonly string[] AllowedTags =
        ["p", "br", "strong", "b", "em", "i", "u", "s", "ul", "ol", "li", "h3", "h4", "blockquote", "a", "span"];

    private readonly HtmlSanitizer _sanitizer = Create();

    public string Sanitize(string html) => _sanitizer.Sanitize(html ?? string.Empty).Trim();

    private static HtmlSanitizer Create()
    {
        var options = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(AllowedTags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(["href", "title"], StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(["http", "https", "mailto"], StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(["href"], StringComparer.OrdinalIgnoreCase),
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
