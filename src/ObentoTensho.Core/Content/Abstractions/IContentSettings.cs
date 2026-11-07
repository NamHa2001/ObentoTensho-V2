namespace ObentoTensho.Core.Content.Abstractions;

public interface IContentSettings
{
    // SEC-47: miền https được phép làm liên kết CTA banner
    IReadOnlyCollection<string> AllowedCtaHosts { get; }
}
