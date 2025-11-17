namespace PostyFox_Secrets;

public interface ISecureStore
{
    /// <summary>
    /// Get a secret value by name. Returns null if not found.
    /// </summary>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a secret value by name.
    /// </summary>
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a secret by name.
    /// </summary>
    Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default);
}
