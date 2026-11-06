using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

// D-02: dùng bộ băm PBKDF2-HMAC-SHA512 của ASP.NET Core Identity (định dạng V3, muối 128 bit)
public sealed class Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options) : Core.Auth.Abstractions.IPasswordHasher
{
    private static readonly object Owner = new();

    private readonly PasswordHasher<object> _hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = options.Value.Iterations,
    }));

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _hasher.HashPassword(Owner, password);
    }

    public PasswordCheckResult Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(password))
        {
            return PasswordCheckResult.Failed;
        }

        PasswordVerificationResult result;
        try
        {
            result = _hasher.VerifyHashedPassword(Owner, passwordHash, password);
        }
        catch (FormatException)
        {
            // Bản băm hỏng trong CSDL: coi như sai mật khẩu, không để lộ lỗi 500
            return PasswordCheckResult.Failed;
        }

        return result switch
        {
            PasswordVerificationResult.Success => PasswordCheckResult.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
            _ => PasswordCheckResult.Failed,
        };
    }
}
