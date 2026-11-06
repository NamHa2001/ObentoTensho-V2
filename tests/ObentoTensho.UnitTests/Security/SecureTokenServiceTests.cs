using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.UnitTests.Security;

public sealed class SecureTokenServiceTests
{
    private readonly SecureTokenService _service = new();

    [Fact]
    public void Token_is_url_safe_and_256_bit()
    {
        var token = _service.Create();

        // 32 byte Base64Url không đệm = 43 ký tự
        Assert.Equal(43, token.Value.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token.Value);
    }

    [Fact]
    public void Hash_is_lowercase_sha256_hex_and_matches_value()
    {
        var token = _service.Create();

        Assert.Matches("^[0-9a-f]{64}$", token.Hash);
        Assert.Equal(token.Hash, _service.Hash(token.Value));
    }

    [Fact]
    public void Hash_matches_known_vector()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", _service.Hash("abc"));
    }

    [Fact]
    public void Tokens_are_unique()
    {
        var values = Enumerable.Range(0, 1000).Select(_ => _service.Create().Value).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(1000, values.Count);
    }

    [Fact]
    public void HashEquals_compares_exactly()
    {
        var hash = _service.Hash("abc");

        Assert.True(_service.HashEquals(hash, _service.Hash("abc")));
        Assert.False(_service.HashEquals(hash, _service.Hash("abd")));
        Assert.False(_service.HashEquals(hash, hash[..^1]));
    }

    [Fact]
    public void SecurityStamp_is_128_bit_hex()
    {
        var stamp = _service.NewSecurityStamp();

        Assert.Matches("^[0-9a-f]{32}$", stamp);
        Assert.NotEqual(stamp, _service.NewSecurityStamp(), StringComparer.Ordinal);
    }
}
