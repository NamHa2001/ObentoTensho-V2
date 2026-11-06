namespace ObentoTensho.Contracts.Enums;

// docs/srs/08 EmailLogs.Status; Suppressed: không gửi vì vượt giới hạn theo địa chỉ nhận (SEC-45)
public enum EmailStatus : byte
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Suppressed = 3
}
