using FluentValidation.Results;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Common;

public static class ValidationResultExtensions
{
    // Tên trường camelCase như JSON, giá trị là mã lỗi để frontend dịch (docs/srs/09 §2)
    public static Result<T> ToFailure<T>(this ValidationResult validation) =>
        Result.Failure<T>(ErrorCodes.ValidationFailed, ToFieldErrors(validation));

    public static Result ToFailure(this ValidationResult validation) =>
        Result.Failure(ErrorCodes.ValidationFailed, ToFieldErrors(validation));

    private static Dictionary<string, string[]> ToFieldErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
