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

    // docs/srs/09 §4: ETag/If-Match không khớp (người khác vừa sửa) hoặc thiếu If-Match
    public const string PreconditionFailed = "PRECONDITION_FAILED";
    public const string PreconditionRequired = "PRECONDITION_REQUIRED";

    // Bàn, ca, giờ mở cửa (docs/srs/16)
    public const string DuplicateName = "DUPLICATE_NAME";
    public const string AreaHasTables = "AREA_HAS_TABLES";
    public const string AreaInactive = "AREA_INACTIVE";
    public const string SlotOverlap = "SLOT_OVERLAP";
    public const string SlotOutsideOpeningHours = "SLOT_OUTSIDE_OPENING_HOURS";
    public const string InternalError = "INTERNAL_ERROR";
}
