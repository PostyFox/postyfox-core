using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using System.Threading.Tasks;

namespace PostyFox_Secrets;

public class KeyVaultStore : IKeyVaultStore
{
    private readonly SecretClient _client;
    public string VaultUri { get; }

    public KeyVaultStore(SecretClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        VaultUri = client.VaultUri.ToString();
    }

    public KeyVaultStore(string vaultUri, DefaultAzureCredentialOptions? credentialOptions = null)
    {
        if (string.IsNullOrEmpty(vaultUri)) throw new ArgumentNullException(nameof(vaultUri));
        VaultUri = vaultUri;
        var cred = credentialOptions == null ? new DefaultAzureCredential() : new DefaultAzureCredential(credentialOptions);
        _client = new SecretClient(new Uri(vaultUri), cred);
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var resp = await _client.GetSecretAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
            return resp?.Value?.Value;
        }
        catch (Azure.RequestFailedException)
        {
            return null;
        }
    }

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        await _client.SetSecretAsync(name, value, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        await _client.StartDeleteSecretAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
