namespace ObentoTensho.Contracts.Enums;

// BR-LOY-21: điều chỉnh điểm lớn chờ người thứ hai duyệt
public enum AdjustmentStatus : byte
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
