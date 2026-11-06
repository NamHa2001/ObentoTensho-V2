namespace ObentoTensho.Core.Auth.Abstractions;

// docs/srs/08: khóa TOTP lưu mã hóa (Data Protection), lộ CSDL không đủ để sinh mã 2FA
public interface ITotpSecretProtector
{
    byte[] Protect(byte[] secret);

    byte[]? Unprotect(byte[] protectedSecret);
}
