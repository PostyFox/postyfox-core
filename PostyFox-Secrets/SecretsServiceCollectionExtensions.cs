using Microsoft.Extensions.DependencyInjection;
using Azure.Identity;

namespace PostyFox_Secrets;

public static class SecretsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a secure secret store implementation based on provided configuration.
    /// Priority: Key Vault (vaultUri) else Infisical (infisicalBaseUrl).
    /// </summary>
    public static IServiceCollection AddSecureStore(this IServiceCollection services, string? vaultUri, string? infisicalBaseUrl, string? infisicalApiKey, DefaultAzureCredentialOptions? credentialOptions = null, bool infisicalUseManagedIdentity = false, string? infisicalScope = null)
    {
        if (!string.IsNullOrEmpty(vaultUri))
        {
            services.AddSingleton<IKeyVaultStore>(sp => new KeyVaultStore(vaultUri, credentialOptions));
            services.AddSingleton<ISecureStore>(sp => sp.GetRequiredService<IKeyVaultStore>());
        }
        else if (!string.IsNullOrEmpty(infisicalBaseUrl))
        {
            // Register Infisical HTTP client. If managed identity is requested, do not set Authorization header here.
            services.AddHttpClient<IInfisicalStore, InfisicalStore>(client =>
            {
                client.BaseAddress = new Uri(infisicalBaseUrl);
                if (!infisicalUseManagedIdentity && !string.IsNullOrEmpty(infisicalApiKey)) client.DefaultRequestHeaders.Add("Authorization", $"Bearer {infisicalApiKey}");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler());

            // Register with managed identity flag
            services.AddSingleton<IInfisicalStore>(sp =>
            {
                var http = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient(typeof(InfisicalStore).FullName);
                return new InfisicalStore(http, infisicalUseManagedIdentity, infisicalScope);
            });

            services.AddSingleton<ISecureStore>(sp => sp.GetRequiredService<IInfisicalStore>());
        }

        return services;
    }
}
