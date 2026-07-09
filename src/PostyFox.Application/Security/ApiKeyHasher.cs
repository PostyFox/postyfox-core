using System.Security.Cryptography;
using PostyFox.Application.Abstractions;

namespace PostyFox.Application.Security;

/// <summary>
/// PBKDF2 (SHA-256) API key hasher. Format: <c>v1.{iterations}.{saltB64}.{hashB64}</c>.
/// Verification is constant-time.
/// </summary>
public sealed class ApiKeyHasher : IApiKeyHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string Hash(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(key, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string key, string hash)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(hash)) return false;
        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != "v1") return false;
        if (!int.TryParse(parts[1], out var iterations)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(key, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
