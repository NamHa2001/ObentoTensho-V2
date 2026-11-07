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
    public const string TableHasReservations = "TABLE_HAS_RESERVATIONS";
    public const string SlotHasReservations = "SLOT_HAS_RESERVATIONS";

    // Đặt bàn (docs/srs/11)
    public const string SlotFull = "SLOT_FULL";
    public const string DateOutOfRange = "DATE_OUT_OF_RANGE";
    public const string OutsideOpeningHours = "OUTSIDE_OPENING_HOURS";
    public const string OutsideServiceSlot = "OUTSIDE_SERVICE_SLOT";
    public const string DateBlocked = "DATE_BLOCKED";
    public const string ReservationLimit = "RSV_LIMIT";
    public const string OtpRequired = "OTP_REQUIRED";

    // Mã OTP sai, hết hạn hoặc đã hủy vì sai quá số lần (BR-AUTH-18)
    public const string OtpInvalid = "OTP_INVALID";

    // Turnstile không hợp lệ hoặc dịch vụ kiểm tra không phản hồi (SEC-57)
    public const string CaptchaFailed = "CAPTCHA_FAILED";
    public const string InvalidTransition = "INVALID_TRANSITION";
    public const string ContactNotConfirmed = "CONTACT_NOT_CONFIRMED";
    public const string CallAttemptsExceeded = "CALL_ATTEMPTS_EXCEEDED";
    public const string TableConflict = "TABLE_CONFLICT";
    public const string TableUnsuitable = "TABLE_UNSUITABLE";

    // Thực đơn (docs/srs/15)
    public const string CategoryHasItems = "CATEGORY_HAS_ITEMS";
    public const string CategoryInactive = "CATEGORY_INACTIVE";
    public const string OptionGroupInUse = "OPTION_GROUP_IN_USE";
    public const string OptionInvalid = "OPTION_INVALID";
    public const string OptionUnavailable = "OPTION_UNAVAILABLE";

    // Tệp tải lên (BR-MNU-14): sai định dạng/mẫu, hoặc vượt giới hạn dung lượng/số dòng
    public const string FileInvalid = "FILE_INVALID";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    // Đặt món mang về (docs/srs/12)
    public const string ItemUnavailable = "ITEM_UNAVAILABLE";
    public const string OrderLimit = "ORD_LIMIT";
    public const string PaymentRequired = "PAYMENT_REQUIRED";
    public const string InternalError = "INTERNAL_ERROR";
}
