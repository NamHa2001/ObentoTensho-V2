namespace ObentoTensho.Infrastructure.Media;

// Thư mục ảnh và địa chỉ phục vụ (FR-IMG-04). Máy chủ: PublicBaseUrl là tên miền tĩnh không cookie (SEC-56)
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public string RootPath { get; set; } = "App_Data/media";

    public string PublicBaseUrl { get; set; } = "/uploads";

    public string PublicDirectory => Path.GetFullPath(Path.Combine(RootPath, "public"));

    public string OriginalsDirectory => Path.GetFullPath(Path.Combine(RootPath, "originals"));
}
