using System.Collections.Frozen;
using System.Net;
using System.Text.RegularExpressions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Infrastructure.Email;

// Mẫu nhúng trong assembly: Email/Templates/{loại}.{ngôn ngữ}.txt gồm 3 phần ngăn bằng "===":
// "subject: …", bản text thuần, đoạn HTML. Biến {{tên}} được escape trong HTML (SEC-59).
internal sealed partial class EmbeddedEmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string ResourcePrefix = "EmailTemplates.";
    private const string SectionSeparator = "\n===\n";

    private static readonly Lazy<FrozenDictionary<string, EmailTemplate>> Templates = new(Load);

    public static IReadOnlyCollection<string> Keys => Templates.Value.Keys;

    public EmailMessage Render(EmailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = EmailTypes.Of(request.Kind);
        var language = AuthPolicy.SupportedLanguages.Contains(request.Language, StringComparer.Ordinal) ? request.Language : AuthPolicy.DefaultLanguage;
        if (!Templates.Value.TryGetValue($"{type}.{language}", out var template))
        {
            throw new InvalidOperationException($"Thiếu mẫu email {type}.{language}.");
        }

        // Chống chèn header: tiêu đề không được có xuống dòng
        var subject = Fill(template.Subject, request.Values, encode: false).ReplaceLineEndings(" ");
        var text = Fill(template.Text, request.Values, encode: false);
        var html = EmailLayout.Wrap(language, subject, Fill(template.Html, request.Values, encode: true));
        return new EmailMessage(request.To, subject, html, text);
    }

    private static string Fill(string template, IReadOnlyDictionary<string, string> values, bool encode) =>
        Placeholder().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            if (!values.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException($"Thiếu biến {{{{{name}}}}} cho mẫu email.");
            }

            return encode ? WebUtility.HtmlEncode(value) : value;
        });

    private static FrozenDictionary<string, EmailTemplate> Load()
    {
        var assembly = typeof(EmbeddedEmailTemplateRenderer).Assembly;
        var templates = new Dictionary<string, EmailTemplate>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name);
            using var reader = new StreamReader(stream);
            var parts = reader.ReadToEnd().ReplaceLineEndings("\n").Split(SectionSeparator);
            if (parts.Length != 3 || !parts[0].StartsWith("subject: ", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Mẫu email {name} sai định dạng.");
            }

            var key = name[ResourcePrefix.Length..^".txt".Length];
            templates[key] = new EmailTemplate(parts[0]["subject: ".Length..].Trim(), parts[1].Trim(), parts[2].Trim());
        }

        return templates.ToFrozenDictionary(StringComparer.Ordinal);
    }

    [GeneratedRegex(@"\{\{(?<name>[A-Za-z]+)\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();

    private sealed record EmailTemplate(string Subject, string Text, string Html);
}
