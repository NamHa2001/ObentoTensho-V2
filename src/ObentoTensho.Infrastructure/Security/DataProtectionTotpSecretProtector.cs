using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

// Khóa Data Protection phải được giữ bền (thư mục/volume riêng): mất khóa thì mọi nhân viên phải thiết lập lại 2FA
internal sealed class DataProtectionTotpSecretProtector(IDataProtectionProvider provider) : ITotpSecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("ObentoTensho.StaffTotpSecret.v1");

    public byte[] Protect(byte[] secret) => _protector.Protect(secret);

    public byte[]? Unprotect(byte[] protectedSecret)
    {
        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
