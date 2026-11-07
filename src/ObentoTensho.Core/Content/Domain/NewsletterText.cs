using System.Net;
using System.Text.RegularExpressions;

namespace ObentoTensho.Core.Content.Domain;

// Bản text thuần của bản tin (BR-NTF-10: thư luôn có bản text)
public static partial class NewsletterText
{
    public static string FromHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var withBreaks = BlockEnd().Replace(html, "\n");
        return WebUtility.HtmlDecode(Tag().Replace(withBreaks, string.Empty)).Trim();
    }

    [GeneratedRegex(@"</(p|li|h2|h3|h4|blockquote)>|<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex BlockEnd();

    [GeneratedRegex("<[^>]+>", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Tag();
}
