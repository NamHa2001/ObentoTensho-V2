using Microsoft.Extensions.Options;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.UnitTests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    private const string Password = "Mật-khẩu đủ dài 2026";

    private static Pbkdf2PasswordHasher Create(int iterations) =>
        new(Options.Create(new PasswordHashingOptions { Iterations = iterations }));

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hasher = Create(PasswordHashingOptions.MinimumIterations);

        var hash = hasher.Hash(Password);

        Assert.Equal(PasswordCheckResult.Success, hasher.Verify(hash, Password));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        var hasher = Create(PasswordHashingOptions.MinimumIterations);

        var hash = hasher.Hash(Password);

        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify(hash, Password + "x"));
    }

    [Fact]
    public void Same_password_gets_different_salt()
    {
        var hasher = Create(PasswordHashingOptions.MinimumIterations);

        Assert.NotEqual(hasher.Hash(Password), hasher.Hash(Password), StringComparer.Ordinal);
    }

    [Fact]
    public void Hash_made_with_fewer_iterations_needs_rehash()
    {
        var oldHash = Create(PasswordHashingOptions.MinimumIterations).Hash(Password);

        var result = Create(PasswordHashingOptions.MinimumIterations + 1).Verify(oldHash, Password);

        Assert.Equal(PasswordCheckResult.SuccessRehashNeeded, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("không-phải-base64")]
    [InlineData("AAAA")]
    public void Malformed_hash_fails_without_throwing(string hash)
    {
        var hasher = Create(PasswordHashingOptions.MinimumIterations);

        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify(hash, Password));
    }

    [Fact]
    public void Empty_password_is_rejected()
    {
        var hasher = Create(PasswordHashingOptions.MinimumIterations);

        Assert.Throws<ArgumentException>(() => hasher.Hash(string.Empty));
        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify(hasher.Hash(Password), string.Empty));
    }

    [Fact]
    public void Default_iterations_meet_minimum()
    {
        Assert.True(new PasswordHashingOptions().Iterations >= PasswordHashingOptions.MinimumIterations);
    }
}
