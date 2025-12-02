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

var tableAccount = Environment.GetEnvironmentVariable("ConfigTable") ?? throw new Exception("Configuration not found for ConfigTable");
var storageAccount = Environment.GetEnvironmentVariable("StorageAccount") ?? throw new Exception("Configuration not found for StorageAccount");
var queueAccount = Environment.GetEnvironmentVariable("PostingQueue") ?? throw new Exception("Configuration not found for PostingQueue");

var twitchClientId = Environment.GetEnvironmentVariable("TwitchClientId") ?? throw new Exception("Configuration not found for TwitchClientId");
var twitchCallbackUrl = Environment.GetEnvironmentVariable("TwitchCallbackUrl") ?? throw new Exception("Configuration not found for TwitchCallbackUrl");

var defaultCredentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeVisualStudioCredential = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode")),
    ExcludeManagedIdentityCredential = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode"))
};

var twitchClientSecret = "";
var twitchSignatureSecret = "";

var secretStoreUri = Environment.GetEnvironmentVariable("SecretStore");
if (!string.IsNullOrEmpty(secretStoreUri))
{
    // Attempt to read secrets later via DI-registered provider
}

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker => worker.UseNewtonsoftJson())
    .ConfigureServices(services =>
    {
        services.AddAzureClients(clientBuilder =>
        {
            // Register clients for each service
#pragma warning disable CS8604
            clientBuilder.AddTableServiceClient(new Uri(tableAccount)).WithName("ConfigTable");
            clientBuilder.AddBlobServiceClient(new Uri(storageAccount)).WithName("StorageAccount");
            clientBuilder.AddQueueServiceClient(new Uri(queueAccount)).WithName("PostingQueue");
#pragma warning restore CS8604
            
            clientBuilder.UseCredential(new DefaultAzureCredential(defaultCredentialOptions));
        });

        // Register adapters-secrets providers
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

        if (!string.IsNullOrEmpty(twitchClientId) && !string.IsNullOrEmpty(twitchClientSecret) &&
            !string.IsNullOrEmpty(twitchCallbackUrl) && !string.IsNullOrEmpty(twitchSignatureSecret))
        {
            services.AddTwitchEventSubService(config =>
            {
                config.ClientId = twitchClientId;
                config.ClientSecret = twitchClientSecret;
                config.CallbackUrl = twitchCallbackUrl;
                config.SignatureSecret = twitchSignatureSecret;
            });

            services.AddTwitchApiClient(config =>
            {
                config.ClientId = twitchClientId;
                config.ClientSecret = twitchClientSecret;
            });
        } else
        {
            Console.Write("TWITCH NOT FULLY CONFIGURED");
        }

        //services.AddHostedService<TwitchNotificationService>();
        //services.AddTransient<EventSubBuilder>();

    })
    .Build();

host.Run();
