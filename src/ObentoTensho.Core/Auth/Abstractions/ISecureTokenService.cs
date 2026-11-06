namespace ObentoTensho.Core.Auth.Abstractions;

// SEC-05, SEC-07: token sinh bằng CSPRNG, chỉ lưu bản băm; so sánh hằng thời gian (SEC-65)
public interface ISecureTokenService
{
    SecureToken Create();

    string Hash(string token);

    bool HashEquals(string expectedHash, string actualHash);

    string NewSecurityStamp();
}
