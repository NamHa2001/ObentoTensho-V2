namespace ObentoTensho.Core.Common;

// Tạo bằng Result.Success<T>/Result.Failure<T>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, string? errorCode, IReadOnlyDictionary<string, string[]>? fieldErrors)
        : base(errorCode, fieldErrors)
    {
        _value = value;
    }

    public T Value => IsSuccess && _value is not null
        ? _value
        : throw new InvalidOperationException("Kết quả thất bại không có giá trị.");
}
