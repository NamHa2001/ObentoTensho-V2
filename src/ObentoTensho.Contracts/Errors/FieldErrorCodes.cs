namespace ObentoTensho.Contracts.Errors;

/// <summary>Mã lỗi theo từng trường trong <c>errors</c> của ProblemDetails; frontend dịch theo mã.</summary>
public static class FieldErrorCodes
{
    public const string Required = "REQUIRED";
    public const string TooShort = "TOO_SHORT";
    public const string TooLong = "TOO_LONG";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string PasswordTooCommon = "PASSWORD_TOO_COMMON";
    public const string PasswordContainsEmail = "PASSWORD_CONTAINS_EMAIL";
    public const string SameAsCurrent = "SAME_AS_CURRENT";
}
