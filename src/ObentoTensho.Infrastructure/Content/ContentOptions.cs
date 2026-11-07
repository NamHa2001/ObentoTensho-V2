using ObentoTensho.Core.Content.Abstractions;

namespace ObentoTensho.Infrastructure.Content;

// SEC-47: danh sách miền https được phép cho liên kết CTA
public sealed class ContentOptions : IContentSettings
{
    public const string SectionName = "Content";

    public string[] AllowedCtaHostList { get; set; } = [];

    public IReadOnlyCollection<string> AllowedCtaHosts => AllowedCtaHostList;
}
