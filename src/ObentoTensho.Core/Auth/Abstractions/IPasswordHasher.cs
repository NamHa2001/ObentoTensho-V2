namespace ObentoTensho.Core.Auth.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheckResult Verify(string passwordHash, string password);
}
