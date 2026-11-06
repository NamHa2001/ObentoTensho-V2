namespace ObentoTensho.Core.Common;

// Trùng khóa duy nhất do hai yêu cầu chạy đồng thời; tầng nghiệp vụ quyết định cách phản hồi
public sealed class DuplicateKeyException : Exception
{
    public DuplicateKeyException()
    {
    }

    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    public DuplicateKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
