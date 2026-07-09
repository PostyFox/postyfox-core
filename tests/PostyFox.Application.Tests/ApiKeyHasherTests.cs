using PostyFox.Application.Security;
using Xunit;

namespace PostyFox.Application.Tests;

public class ApiKeyHasherTests
{
    private readonly ApiKeyHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = _hasher.Hash("super-secret-key");
        Assert.True(_hasher.Verify("super-secret-key", hash));
    }

    [Fact]
    public void Verify_fails_for_wrong_key()
    {
        var hash = _hasher.Hash("correct");
        Assert.False(_hasher.Verify("incorrect", hash));
    }

    [Fact]
    public void Same_key_hashes_differ_due_to_salt()
    {
        Assert.NotEqual(_hasher.Hash("k"), _hasher.Hash("k"));
    }

    [Fact]
    public void Verify_fails_for_malformed_hash()
    {
        Assert.False(_hasher.Verify("k", "not-a-valid-hash"));
    }
}
