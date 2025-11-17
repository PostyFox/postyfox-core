using System.Net.Http.Json;
using System.Net.Http.Headers;
using Azure.Identity;
using Azure.Core;

namespace PostyFox_Secrets;

public class InfisicalStore : IInfisicalStore
{
    private readonly HttpClient _httpClient;
    public string? Workspace { get; set; }
    private readonly bool _useManagedIdentity;
    private readonly DefaultAzureCredential? _credential;
    private readonly string? _scope;

    public InfisicalStore(HttpClient httpClient, bool useManagedIdentity = false, string? scope = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _useManagedIdentity = useManagedIdentity;
        _scope = scope;
        if (_useManagedIdentity)
        {
            _credential = new DefaultAzureCredential();
        }
    }

    private async Task EnsureManagedIdentityHeaderAsync(CancellationToken cancellationToken)
    {
        if (!_useManagedIdentity || _credential == null) return;
        string scope = _scope ?? "https://infisical.com/.default";
        var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken).ConfigureAwait(false);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureManagedIdentityHeaderAsync(cancellationToken).ConfigureAwait(false);
        var endpoint = $"/api/v1/secrets/{name}";
        var resp = await _httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        var doc = await resp.Content.ReadFromJsonAsync<InfisicalSecretResponse?>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return doc?.Value;
    }

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        await EnsureManagedIdentityHeaderAsync(cancellationToken).ConfigureAwait(false);
        var endpoint = $"/api/v1/secrets";
        var payload = new { key = name, value };
        await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureManagedIdentityHeaderAsync(cancellationToken).ConfigureAwait(false);
        var endpoint = $"/api/v1/secrets/{name}";
        await _httpClient.DeleteAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    private class InfisicalSecretResponse
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
    }
}
