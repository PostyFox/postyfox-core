using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Infrastructure.Persistence;

namespace PostyFox.Infrastructure.Secrets;

public sealed class SecretStoreOptions
{
    public const string SectionName = "Secrets";
    /// <summary>Base64-encoded 32-byte AES key (envelope/KEK). Swap for Vault/KMS in production.</summary>
    public string EncryptionKey { get; set; } = string.Empty;
}

/// <summary>
/// Secret store backed by Postgres with AES-256-GCM encryption at rest. This is the
/// cloud-agnostic default for local/dev; production swaps in Vault/Infisical/KMS.
/// </summary>
public sealed class EncryptedDbSecretStore : ISecretStore
{
    private readonly AppDbContext _db;
    private readonly byte[] _key;

    public EncryptedDbSecretStore(AppDbContext db, IOptions<SecretStoreOptions> options)
    {
        _db = db;
        var raw = options.Value.EncryptionKey;
        _key = string.IsNullOrEmpty(raw)
            ? SHA256.HashData(Encoding.UTF8.GetBytes("postyfox-dev-insecure-key"))
            : Convert.FromBase64String(raw);
        if (_key.Length != 32) throw new InvalidOperationException("Secrets:EncryptionKey must be 32 bytes (base64).");
    }

    public async Task SetSecretAsync(string name, string value, CancellationToken ct = default)
    {
        var cipher = Encrypt(value);
        var entry = await _db.Secrets.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (entry is null)
        {
            entry = new SecretEntry { Name = name };
            _db.Secrets.Add(entry);
        }
        entry.CipherText = cipher;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken ct = default)
    {
        var entry = await _db.Secrets.FirstOrDefaultAsync(s => s.Name == name, ct);
        return entry is null ? null : Decrypt(entry.CipherText);
    }

    public async Task DeleteSecretAsync(string name, CancellationToken ct = default)
    {
        var entry = await _db.Secrets.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (entry is not null)
        {
            _db.Secrets.Remove(entry);
            await _db.SaveChangesAsync(ct);
        }
    }

    private string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    private string Decrypt(string encoded)
    {
        var blob = Convert.FromBase64String(encoded);
        var nonceLen = AesGcm.NonceByteSizes.MaxSize;
        var tagLen = AesGcm.TagByteSizes.MaxSize;
        var nonce = blob[..nonceLen];
        var tag = blob[nonceLen..(nonceLen + tagLen)];
        var cipher = blob[(nonceLen + tagLen)..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
