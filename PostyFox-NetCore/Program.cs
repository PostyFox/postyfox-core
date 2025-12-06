using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Twitch.Net.Api;
using Twitch.Net.EventSub;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Neillans.Adapters.Secrets.Core;
using Neillans.Adapters.Secrets.AzureKeyVault;
using Neillans.Adapters.Secrets.Infisical;

// Load the configuration from the environment variables
var tableAccount = Environment.GetEnvironmentVariable("ConfigTable");
var storageAccount = Environment.GetEnvironmentVariable("StorageAccount");

var twitchClientId = Environment.GetEnvironmentVariable("TwitchClientId");
var twitchCallbackUrl = Environment.GetEnvironmentVariable("TwitchCallbackUrl");

var twitchClientSecret = Environment.GetEnvironmentVariable("TwitchClientSecret");
var twitchSignatureSecret = Environment.GetEnvironmentVariable("TwitchSignatureSecret");

var infisicalBaseUrl = Environment.GetEnvironmentVariable("Infisical_Url");
var infisicalApiKey = Environment.GetEnvironmentVariable("Infisical_ApiKey");
var secretStoreUri = Environment.GetEnvironmentVariable("SecretStore");

var defaultCredentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeVisualStudioCredential = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode")),
    ExcludeManagedIdentityCredential = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode"))
};

// Create the host
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker => worker.UseNewtonsoftJson())
    .ConfigureServices(services =>
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register clients for each service (table/blob)
        services.AddAzureClients(clientBuilder =>
        {
#pragma warning disable CS8604
            if (!string.IsNullOrEmpty(tableAccount))
            {
                clientBuilder.AddTableServiceClient(new Uri(tableAccount)).WithName("ConfigTable");
            }

            if (!string.IsNullOrEmpty(storageAccount))
            {
                clientBuilder.AddBlobServiceClient(new Uri(storageAccount)).WithName("StorageAccount");
            }
#pragma warning restore CS8604

            clientBuilder.UseCredential(new DefaultAzureCredential());
        });

        // Register adapters-secrets providers via package extensions
        services.AddSecretsProviderFactory();
        if (!string.IsNullOrEmpty(secretStoreUri))
        {
            // Use extension method from AzureKeyVault adapter package
            services.AddAzureKeyVaultSecretsProvider(options =>
            {
                options.VaultUri = secretStoreUri!;
            });
        }
        else if (!string.IsNullOrEmpty(infisicalBaseUrl))
        {
            // Use extension method from Infisical adapter package
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

        // Configure Twitch clients
        if (!string.IsNullOrEmpty(twitchClientId) && !string.IsNullOrEmpty(twitchClientSecret))
        {
            services.AddTwitchApiClient(config =>
            {
                config.ClientId = twitchClientId;
                config.ClientSecret = twitchClientSecret;
            });
        }

        if (!string.IsNullOrEmpty(twitchClientId) && !string.IsNullOrEmpty(twitchClientSecret) &&
            !string.IsNullOrEmpty(twitchCallbackUrl) && !string.IsNullOrEmpty(twitchSignatureSecret))
        {
            services.AddTwitchEventSubService(config =>
            {
                config.SignatureSecret = twitchSignatureSecret;
                config.ClientId = twitchClientId;
                config.ClientSecret = twitchClientSecret;
                config.CallbackUrl = twitchCallbackUrl;
            });
        }
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
    })
    .Build();

// Resolve secure store from DI and fetch secrets if missing
using (var scope = host.Services.CreateScope())
{
    var provider = scope.ServiceProvider.GetService<ISecretsProvider>();
    if (provider != null)
    {
        if (string.IsNullOrEmpty(twitchClientSecret))
        {
            twitchClientSecret = provider.GetSecretAsync("TwitchClientSecret").GetAwaiter().GetResult();
        }
        if (string.IsNullOrEmpty(twitchSignatureSecret))
        {
            twitchSignatureSecret = provider.GetSecretAsync("TwitchSignatureSecret").GetAwaiter().GetResult();
        }
    }
}

ILogger logger = host.Services.GetService<ILogger<Program>>();

logger.LogInformation("Starting PostyFox-NetCore");
logger.LogInformation("Configuration loaded");

logger.LogInformation("Table Account: {tableAccount}", tableAccount);
logger.LogInformation("Storage Account: {storageAccount}", storageAccount);
logger.LogInformation("Twitch Client ID: {twitchClientId}", twitchClientId);
logger.LogInformation("Twitch Callback URL: {twitchCallbackUrl}", twitchCallbackUrl);

if (string.IsNullOrEmpty(twitchClientSecret))
{
    logger.LogInformation("Twitch Client Secret: NOT DEFINED");
}
if (string.IsNullOrEmpty(twitchSignatureSecret))
{
    logger.LogInformation("Twitch Signature Secret: NOT DEFINED");
}
if (string.IsNullOrEmpty(secretStoreUri))
{
    if (!string.IsNullOrEmpty(infisicalBaseUrl))
    {
        logger.LogInformation("Using Infisical secret store: {infisical}", infisicalBaseUrl);
    }
    else
    {
        logger.LogInformation("Secret Store: NOT DEFINED");
    }
}
else
{
    logger.LogInformation("Using Key Vault secret store: {secretStore}", secretStoreUri);
}

await host.RunAsync();