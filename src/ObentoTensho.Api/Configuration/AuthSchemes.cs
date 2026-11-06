namespace ObentoTensho.Api.Configuration;

// Hai scheme JWT với audience khác nhau: token khách không dùng được ở API quản trị và ngược lại
public static class AuthSchemes
{
    public const string Customer = "customer";
    public const string Staff = "staff";
}
