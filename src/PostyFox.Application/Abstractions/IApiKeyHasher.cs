namespace PostyFox.Application.Abstractions;

/// <summary>Hashes and verifies API key secrets. Secrets are never stored in the clear.</summary>
public interface IApiKeyHasher
{
    string Hash(string key);
    bool Verify(string key, string hash);
}
