using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Twitch.Net.Api;
using Azure.Security.KeyVault.Secrets;

var tableAccount = Environment.GetEnvironmentVariable("ConfigTable") ?? throw new Exception("Configuration not found for ConfigTable");
var storageAccount = Environment.GetEnvironmentVariable("StorageAccount") ?? throw new Exception("Configuration not found for StorageAccount");

var twitchClientId = Environment.GetEnvironmentVariable("TwitchClientId") ?? throw new Exception("Configuration not found for TwitchClientId");
var twitchCallbackUrl = Environment.GetEnvironmentVariable("TwitchCallbackUrl") ?? throw new Exception("Configuration not found for TwitchCallbackUrl");

var defaultCredentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeVisualStudioCredential = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode")),
    ExcludeManagedIdentityCredential = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode"))
};

var twitchClientSecret = "";

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SecretStore")))
{
    // Connect to the Secret service and pull the Twitch Secrets, as we need them during initialisation
    SecretClient _secretStore = new SecretClient(new Uri(Environment.GetEnvironmentVariable("SecretStore")), new DefaultAzureCredential(defaultCredentialOptions));
    twitchClientSecret = _secretStore.GetSecret("TwitchClientSecret").Value.ToString();
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureServices(services =>
    {
        services.AddAzureClients(clientBuilder =>
        {
            // Register clients for each service
#pragma warning disable CS8604
            clientBuilder.AddTableServiceClient(new Uri(tableAccount)).WithName("ConfigTable");
            clientBuilder.AddBlobServiceClient(new Uri(storageAccount)).WithName("StorageAccount");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SecretStore")))
            {
                clientBuilder.AddSecretClient(new Uri(Environment.GetEnvironmentVariable("SecretStore"))).WithName("SecretStore");
            }
#pragma warning restore CS8604

            clientBuilder.UseCredential(new DefaultAzureCredential(defaultCredentialOptions));
        });

        services.AddTwitchApiClient(config =>
        {
            config.ClientId = twitchClientId;
            config.ClientSecret = twitchClientSecret;
        });

    })
    .Build();

host.Run();
