using FluentValidation;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Common;

// Tên người không bao giờ có dấu < >: chặn ngay từ đầu vào để tên không thể thành mã HTML ở bất cứ đâu hiển thị nó
public static class TextRules
{
    private static readonly char[] Markup = ['<', '>'];

    public static IRuleBuilderOptions<T, string> NoMarkup<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => v is null || v.IndexOfAny(Markup) < 0).WithErrorCode(FieldErrorCodes.InvalidFormat);
}
