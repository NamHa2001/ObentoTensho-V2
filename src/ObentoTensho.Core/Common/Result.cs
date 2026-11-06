namespace ObentoTensho.Core.Common;

// Kết quả nghiệp vụ không dùng ngoại lệ (docs/srs/95 §3.2): thành công hoặc errorCode kèm lỗi theo trường
public class Result
{
    private static readonly Dictionary<string, string[]> NoErrors = new(StringComparer.Ordinal);

    protected Result(string? errorCode, IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
        ErrorCode = errorCode;
        FieldErrors = fieldErrors ?? NoErrors;
    }

    public bool IsSuccess => ErrorCode is null;

    public string? ErrorCode { get; }

    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    public static Result Success() => new(null, null);

    public static Result Failure(string errorCode, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(errorCode, fieldErrors);

    public static Result<T> Success<T>(T value) => new(value, null, null);

    public static Result<T> Failure<T>(string errorCode, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(default, errorCode, fieldErrors);
}
