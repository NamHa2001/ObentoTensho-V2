namespace ObentoTensho.Api.Http;

// SEC-41: cookie host-only, tiền tố __Host- (bắt buộc Secure, Path=/, không Domain); tên riêng cho khách và quản trị
public static class RefreshTokenCookie
{
    public const string CustomerName = "__Host-ot_rt";
    public const string AdminName = "__Host-ot_art";

    public static string? Read(HttpRequest request, string name) =>
        request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    public static void Write(HttpResponse response, string name, string token, DateTime expiresAtUtc) =>
        response.Cookies.Append(name, token, CreateOptions(expiresAtUtc));

    public static void Clear(HttpResponse response, string name) =>
        response.Cookies.Delete(name, CreateOptions(expires: null));

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
