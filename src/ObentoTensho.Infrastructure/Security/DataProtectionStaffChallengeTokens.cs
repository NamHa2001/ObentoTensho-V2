using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

// Mã tạm được mã hóa + xác thực (Data Protection) nên người dùng không đọc hay sửa được nội dung
internal sealed class DataProtectionStaffChallengeTokens(IDataProtectionProvider provider) : IStaffChallengeTokens
{
    private readonly IDataProtector _protector = provider.CreateProtector("ObentoTensho.StaffLoginChallenge.v1");

    public string Protect(StaffChallenge challenge) =>
        _protector.Protect(JsonSerializer.Serialize(challenge, JsonSerializerOptions.Web));

    public StaffChallenge? Unprotect(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<StaffChallenge>(_protector.Unprotect(token), JsonSerializerOptions.Web);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}
