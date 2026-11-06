using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Core.Auth.Services;

// Hoặc đã vào được (Session), hoặc cần làm tiếp một bước (Challenge)
public sealed record StaffSignInResult(AuthSession? Session, IssuedChallenge? Challenge)
{
    public static StaffSignInResult Authenticated(AuthSession session) => new(session, null);

    public static StaffSignInResult Challenged(IssuedChallenge challenge) => new(null, challenge);
}
