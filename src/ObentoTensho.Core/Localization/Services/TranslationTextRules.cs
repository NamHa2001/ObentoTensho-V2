using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Core.Localization.Services;

// docs/srs/22 §6: bản dịch là dữ liệu người dùng — giới hạn độ dài, không ký tự điều khiển hay ký tự đảo chiều chữ
// (chống đánh lừa hiển thị); bản Việt có nội dung thì bản dịch phải có tiêu đề (và nội dung với tin tức/trang tĩnh)
public static class TranslationTextRules
{
    public static IReadOnlyDictionary<string, string[]> Check(SourceText text, ContentShape shape, SourceText source)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(source);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Field(errors, "title", text.Title, shape.TitleMax, required: true);
        Field(errors, "summary", text.Summary, shape.SummaryMax ?? 0, required: false);
        Field(errors, "ctaText", text.CtaText, shape.CtaMax ?? 0, required: false);
        Field(errors, "metaTitle", text.MetaTitle, ContentShape.MetaTitleMax, required: false);
        Field(errors, "metaDescription", text.MetaDescription, ContentShape.MetaDescriptionMax, required: false);

        // Tin tức và trang tĩnh: nội dung là phần chính, không cho bản dịch chỉ có tiêu đề
        var bodyRequired = shape.RichBody && source.BodyHtml is not null;
        if ((bodyRequired && string.IsNullOrWhiteSpace(text.BodyHtml)) || (text.BodyHtml?.Length ?? 0) > ContentShape.BodyMax)
        {
            errors["bodyHtml"] = [string.IsNullOrWhiteSpace(text.BodyHtml) ? FieldErrorCodes.Required : FieldErrorCodes.TooLong];
        }

        return errors;
    }

    public static bool IsSafeText(string value) =>
        !value.Any(c => (char.IsControl(c) && c is not '\n') || c is >= '\u202A' and <= '\u202E' || c is >= '\u2066' and <= '\u2069' || c is '\u200E' or '\u200F');

    private static void Field(Dictionary<string, string[]> errors, string name, string? value, int max, bool required)
    {
        if (value is null)
        {
            if (required)
            {
                errors[name] = [FieldErrorCodes.Required];
            }

            return;
        }

        if (value.Length > max)
        {
            errors[name] = [FieldErrorCodes.TooLong];
        }
        else if (!IsSafeText(value))
        {
            errors[name] = [FieldErrorCodes.InvalidFormat];
        }
    }
}
