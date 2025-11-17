using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Twitch.Net.Api;
using Twitch.Net.EventSub;
using Microsoft.Extensions.DependencyInjection;
using PostyFox_Secrets;

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
    // Use the KeyVaultStore abstraction to fetch initial secrets without referencing SecretClient
    var kv = new KeyVaultStore(secretStoreUri, defaultCredentialOptions);
    twitchClientSecret = kv.GetSecretAsync("TwitchClientSecret").GetAwaiter().GetResult() ?? string.Empty;
    twitchSignatureSecret = kv.GetSecretAsync("TwitchSignatureSecret").GetAwaiter().GetResult() ?? string.Empty;
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

        // Register secret stores for this project
        if (!string.IsNullOrEmpty(secretStoreUri))
        {
            // Register KeyVaultStore directly so callers don't need KeyVault SDK types
            services.AddSingleton<IKeyVaultStore>(sp => new KeyVaultStore(secretStoreUri, defaultCredentialOptions));
            services.AddSingleton<ISecureStore>(sp => sp.GetRequiredService<IKeyVaultStore>());
        }

        var infisicalBaseUrl = Environment.GetEnvironmentVariable("Infisical_Url");
        var infisicalApiKey = Environment.GetEnvironmentVariable("Infisical_ApiKey");
        if (!string.IsNullOrEmpty(infisicalBaseUrl))
        {
            services.AddHttpClient<IInfisicalStore, InfisicalStore>(client =>
            {
                client.BaseAddress = new Uri(infisicalBaseUrl);
                if (!string.IsNullOrEmpty(infisicalApiKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {infisicalApiKey}");
                }
            });

            if (string.IsNullOrEmpty(secretStoreUri))
            {
                services.AddSingleton<ISecureStore>(sp => sp.GetRequiredService<IInfisicalStore>());
            }
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
