namespace ObentoTensho.Contracts.Enums;

// BR-MNU-01: trạng thái bán của món/tùy chọn — xóa món là IsDeleted, không dùng Hidden
public enum SaleStatus : byte
{
    Hidden = 0,
    Available = 1,
    SoldOut = 2
}
