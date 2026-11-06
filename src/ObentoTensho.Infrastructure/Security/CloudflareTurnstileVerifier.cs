using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ObentoTensho.Infrastructure.Security;

// Gọi API siteverify của Cloudflare; token dùng một lần, hết hạn nhanh (Cloudflare tự kiểm)
internal sealed partial class CloudflareTurnstileVerifier(
    HttpClient http,
    IOptions<TurnstileOptions> options,
    ILogger<CloudflareTurnstileVerifier> logger) : ITurnstileVerifier
{
    private static readonly Uri SiteVerify = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
        {
            return false;
        }

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["secret"] = settings.SecretKey ?? string.Empty,
            ["response"] = token,
        };
        if (remoteIp is not null)
        {
            form["remoteip"] = remoteIp;
        }

        try
        {
            using var response = await http.PostAsync(SiteVerify, new FormUrlEncodedContent(form), cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResult>(cancellationToken);
            return result is { Success: true }
                && settings.AllowedHostnames.Contains(result.Hostname ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            LogUnavailable(logger, ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không kiểm được Turnstile; từ chối yêu cầu.")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);

    private sealed record SiteVerifyResult(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("hostname")] string? Hostname);
}
