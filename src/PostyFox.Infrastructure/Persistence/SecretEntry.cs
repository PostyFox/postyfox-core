namespace PostyFox.Infrastructure.Persistence;

/// <summary>Infrastructure-only entity: an encrypted secret row (dev/local secret store).</summary>
public class SecretEntry
{
    public string Name { get; set; } = string.Empty;
    public string CipherText { get; set; } = string.Empty; // base64(nonce|tag|ciphertext)
    public DateTimeOffset UpdatedAt { get; set; }
}
