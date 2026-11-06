using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

public sealed class SecureTokenService : ISecureTokenService
{
    // 256 bit ngẫu nhiên
    private const int TokenBytes = 32;

    public SecureToken Create()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return new SecureToken(value, Hash(value));
    }

    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public bool HashEquals(string expectedHash, string actualHash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expectedHash), Encoding.ASCII.GetBytes(actualHash));

    public string NewSecurityStamp() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}
