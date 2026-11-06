namespace ObentoTensho.Contracts.Errors;

/// <summary>Mã lỗi ổn định trả trong trường <c>errorCode</c> của ProblemDetails (docs/srs/09 §2).</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";

    // Liên kết mời/đặt lại mật khẩu sai, đã dùng hoặc hết hạn
    public const string TokenInvalid = "TOKEN_INVALID";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
}
