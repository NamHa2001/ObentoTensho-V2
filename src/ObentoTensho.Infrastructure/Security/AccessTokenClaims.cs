namespace ObentoTensho.Infrastructure.Security;

// Tên claim tùy biến trong access token (docs/srs/09 §3)
public static class AccessTokenClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string SecurityStamp = "sv";
}
