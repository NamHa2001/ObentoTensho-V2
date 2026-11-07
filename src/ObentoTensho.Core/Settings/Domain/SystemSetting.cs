using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Settings.Domain;

// docs/srs/08 SystemSettings. Kiểu, khoảng, mô tả chép từ danh mục lúc khởi động để nhìn CSDL là hiểu
public sealed class SystemSetting
{
    public required string Key { get; init; }

    public required string Value { get; set; }

    public required SettingType ValueType { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public required string Description { get; set; }

    public bool RequiresStepUp { get; set; }

    public Guid? UpdatedByStaffId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
