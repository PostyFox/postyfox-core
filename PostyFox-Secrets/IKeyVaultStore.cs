using Azure.Security.KeyVault.Secrets;

namespace PostyFox_Secrets;

public interface IKeyVaultStore : ISecureStore
{
    /// <summary>
    /// The Key Vault URI backing this store.
    /// </summary>
    string VaultUri { get; }
}
