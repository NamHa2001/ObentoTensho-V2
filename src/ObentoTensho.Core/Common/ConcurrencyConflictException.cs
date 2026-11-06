namespace ObentoTensho.Core.Common;

// Lưu thất bại vì RowVersion không khớp: người khác vừa sửa hoặc xóa bản ghi
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
