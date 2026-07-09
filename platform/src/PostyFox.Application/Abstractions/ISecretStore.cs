namespace PostyFox.Application.Abstractions;

/// <summary>
/// Cloud-agnostic secret storage. Backed for local/dev by an encrypted store; can be
/// swapped for HashiCorp Vault, Infisical, cloud KMS, etc. without touching callers.
/// </summary>
public interface ISecretStore
{
    Task SetSecretAsync(string name, string value, CancellationToken ct = default);
    Task<string?> GetSecretAsync(string name, CancellationToken ct = default);
    Task DeleteSecretAsync(string name, CancellationToken ct = default);
}
