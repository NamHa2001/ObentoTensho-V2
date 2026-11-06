namespace ObentoTensho.Api.Http;

// SEC-41: cookie host-only, tiền tố __Host- (bắt buộc Secure, Path=/, không Domain); tên riêng cho khách
public static class RefreshTokenCookie
{
    public const string CustomerName = "__Host-ot_rt";

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(CustomerName, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    public static void Write(HttpResponse response, string token, DateTime expiresAtUtc) =>
        response.Cookies.Append(CustomerName, token, CreateOptions(expiresAtUtc));

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(CustomerName, CreateOptions(expires: null));

    private static CookieOptions CreateOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = expires is null ? null : new DateTimeOffset(expires.Value, TimeSpan.Zero),
    };
}
