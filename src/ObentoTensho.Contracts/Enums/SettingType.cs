namespace ObentoTensho.Contracts.Enums;

public enum SettingType
{
    Number,
    Bool,
    Text,
    Url,
    Email,
    Time,
    Choice,
    List,
    Image,
    // Phần trăm có tối đa 2 chữ số thập phân, ví dụ 1.5 (dấu chấm)
    Percent
}
