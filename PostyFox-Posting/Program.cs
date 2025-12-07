using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Twitch.Net.Api;
using Twitch.Net.EventSub;
using Microsoft.Extensions.DependencyInjection;
using Neillans.Adapters.Secrets.Core;
using Neillans.Adapters.Secrets.AzureKeyVault;
using Neillans.Adapters.Secrets.Infisical;
using Microsoft.Extensions.Logging;

var tableAccount = Environment.GetEnvironmentVariable("ConfigTable") ?? throw new Exception("Configuration not found for ConfigTable");
var storageAccount = Environment.GetEnvironmentVariable("StorageAccount") ?? throw new Exception("Configuration not found for StorageAccount");
var queueAccount = Environment.GetEnvironmentVariable("PostingQueue") ?? throw new Exception("Configuration not found for PostingQueue");

var twitchClientId = Environment.GetEnvironmentVariable("TwitchClientId");
var twitchCallbackUrl = Environment.GetEnvironmentVariable("TwitchCallbackUrl");

var twitchClientSecret = Environment.GetEnvironmentVariable("TwitchClientSecret");
var twitchSignatureSecret = Environment.GetEnvironmentVariable("TwitchSignatureSecret");

var defaultCredentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeVisualStudioCredential = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode")),
    ExcludeManagedIdentityCredential = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode"))
};

var twitchClientSecretFromEnv = twitchClientSecret;
var twitchSignatureSecretFromEnv = twitchSignatureSecret;

var secretStoreUri = Environment.GetEnvironmentVariable("SecretStore");

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker => worker.UseNewtonsoftJson())
    .ConfigureServices(services =>
    {
        services.AddAzureClients(clientBuilder =>
        {
#pragma warning disable CS8604
            clientBuilder.AddTableServiceClient(new Uri(tableAccount)).WithName("ConfigTable");
            clientBuilder.AddBlobServiceClient(new Uri(storageAccount)).WithName("StorageAccount");
            clientBuilder.AddQueueServiceClient(new Uri(queueAccount)).WithName("PostingQueue");
#pragma warning restore CS8604

            clientBuilder.UseCredential(new DefaultAzureCredential(defaultCredentialOptions));
        });

        // Register adapters-secrets providers
        services.AddSecretsProviderFactory();

        var infisicalBaseUrl = Environment.GetEnvironmentVariable("Infisical_Url");
        if (!string.IsNullOrEmpty(secretStoreUri))
        {
            services.AddAzureKeyVaultSecretsProvider(options => { options.VaultUri = secretStoreUri!; });
        }
        else if (!string.IsNullOrEmpty(infisicalBaseUrl))
        {
            services.AddInfisicalSecretsProvider(options =>
            {
                options.SiteUrl = infisicalBaseUrl!;
                options.ClientId = Environment.GetEnvironmentVariable("INFISICAL_CLIENT_ID") ?? string.Empty;
                options.ClientSecret = Environment.GetEnvironmentVariable("INFISICAL_CLIENT_SECRET") ?? string.Empty;
                options.ProjectId = Environment.GetEnvironmentVariable("INFISICAL_PROJECT_ID") ?? string.Empty;
                options.Environment = Environment.GetEnvironmentVariable("INFISICAL_ENVIRONMENT") ?? "dev";
                options.SecretPath = Environment.GetEnvironmentVariable("INFISICAL_SECRET_PATH") ?? "/";
            });
        }

        if (!string.IsNullOrEmpty(twitchClientId) && !string.IsNullOrEmpty(twitchClientSecretFromEnv) &&
            !string.IsNullOrEmpty(twitchCallbackUrl) && !string.IsNullOrEmpty(twitchSignatureSecretFromEnv))
        {
            services.AddTwitchEventSubService(config =>
            {
                config.ClientId = twitchClientId;
                config.ClientSecret = twitchClientSecretFromEnv;
                config.CallbackUrl = twitchCallbackUrl;
                config.SignatureSecret = twitchSignatureSecretFromEnv;
            });

            services.AddTwitchApiClient(config =>
            {
                config.ClientId = twitchClientId;
                config.ClientSecret = twitchClientSecretFromEnv;
            });
        }
        else
        {
            // Twitch might be configured later after secrets resolved
        }

    })
    .Build();

// Resolve secrets provider from DI and fetch secrets if missing
using (var scope = host.Services.CreateScope())
{
    var provider = scope.ServiceProvider.GetService<ISecretsProvider>();
    if (provider != null)
    {
        if (string.IsNullOrEmpty(twitchClientSecretFromEnv))
        {
            twitchClientSecretFromEnv = provider.GetSecretAsync("TwitchClientSecret").GetAwaiter().GetResult();
        }
        if (string.IsNullOrEmpty(twitchSignatureSecretFromEnv))
        {
            twitchSignatureSecretFromEnv = provider.GetSecretAsync("TwitchSignatureSecret").GetAwaiter().GetResult();
        }
    }
}

var logger = host.Services.GetService<ILogger<Program>>();

logger?.LogInformation("Starting PostyFox-Posting");
logger?.LogInformation("Configuration loaded");

if (string.IsNullOrEmpty(twitchClientSecretFromEnv) || string.IsNullOrEmpty(twitchSignatureSecretFromEnv))
{
    logger?.LogInformation("Twitch secrets not fully defined at startup");
}

await host.RunAsync();
