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

// Load the configuration from the environment variables
var tableAccount = Environment.GetEnvironmentVariable("ConfigTable");
var storageAccount = Environment.GetEnvironmentVariable("StorageAccount");

var twitchClientId = Environment.GetEnvironmentVariable("TwitchClientId");
var twitchCallbackUrl = Environment.GetEnvironmentVariable("TwitchCallbackUrl");

var twitchClientSecret = Environment.GetEnvironmentVariable("TwitchClientSecret");
var twitchSignatureSecret = Environment.GetEnvironmentVariable("TwitchSignatureSecret");

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
        services.AddAzureClients(clientBuilder =>
        {
            // Register clients for each service
#pragma warning disable CS8604
            if (!string.IsNullOrEmpty(tableAccount))
            {
                clientBuilder.AddTableServiceClient(new Uri(tableAccount)).WithName("ConfigTable");
            }

            if (!string.IsNullOrEmpty(storageAccount))
            {
                clientBuilder.AddBlobServiceClient(new Uri(storageAccount)).WithName("StorageAccount");
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SecretStore")))
            {
                clientBuilder.AddSecretClient(new Uri(Environment.GetEnvironmentVariable("SecretStore"))).WithName("SecretStore");
            }
#pragma warning restore CS8604

            clientBuilder.UseCredential(new DefaultAzureCredential());
        });

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
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SecretStore"))) {
    logger.LogInformation("Secret Store: NOT DEFINED");
} else
{
    logger.LogInformation("Secret Store: {secretStore}", Environment.GetEnvironmentVariable("SecretStore"));
}

host.Run();