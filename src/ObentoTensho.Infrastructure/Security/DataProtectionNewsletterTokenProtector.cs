using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using ObentoTensho.Core.Content.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

internal sealed class DataProtectionNewsletterTokenProtector(IDataProtectionProvider provider) : INewsletterTokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("ObentoTensho.NewsletterUnsubscribeToken.v1");

    public byte[] Protect(string token) => _protector.Protect(Encoding.UTF8.GetBytes(token));

    public string? Unprotect(byte[] protectedToken)
    {
        try
        {
            return Encoding.UTF8.GetString(_protector.Unprotect(protectedToken));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
