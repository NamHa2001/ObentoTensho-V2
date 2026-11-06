namespace ObentoTensho.Core.Auth.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheckResult Verify(string passwordHash, string password);

    // BR-AUTH-14: tốn đúng thời gian như Verify khi không có tài khoản, để không dò được email qua thời gian phản hồi
    void SimulateVerify(string password);
}
