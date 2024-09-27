using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Twitch.Net.Api;
using Twitch.Net.EventSub;
using Azure.Security.KeyVault.Secrets;

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

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SecretStore")))
{
    // Connect to the Secret service and pull the Twitch Secrets, as we need them during initialisation
    SecretClient _secretStore = new SecretClient(new Uri(Environment.GetEnvironmentVariable("SecretStore")), new DefaultAzureCredential(defaultCredentialOptions));
    twitchClientSecret = _secretStore.GetSecret("TwitchClientSecret").Value.ToString();
    twitchSignatureSecret = _secretStore.GetSecret("TwitchSignatureSecret").Value.ToString(); ;
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
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SecretStore")))
            {
                clientBuilder.AddSecretClient(new Uri(Environment.GetEnvironmentVariable("SecretStore"))).WithName("SecretStore");
            }
#pragma warning restore CS8604
            
            clientBuilder.UseCredential(new DefaultAzureCredential(defaultCredentialOptions));
        });

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

        //services.AddHostedService<TwitchNotificationService>();
        //services.AddTransient<EventSubBuilder>();

    })
    .Build();

host.Run();
