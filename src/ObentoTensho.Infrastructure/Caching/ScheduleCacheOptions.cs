namespace ObentoTensho.Infrastructure.Caching;

// Q13: cache các dòng lịch cho đường đọc (chỗ trống, đặt món). Test sửa lịch thẳng trong CSDL thì tắt
public sealed class ScheduleCacheOptions
{
    public const string SectionName = "Caching:Schedule";

    public bool Enabled { get; set; } = true;
}
