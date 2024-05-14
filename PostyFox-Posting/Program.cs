using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;

var tableAccount = Environment.GetEnvironmentVariable("ConfigTable") ?? throw new Exception("Configuration not found for ConfigTable");
var storageAccount = Environment.GetEnvironmentVariable("StorageAccount") ?? throw new Exception("Configuration not found for StorageAccount");

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
            var defaultCredentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCredential = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode")),
                ExcludeManagedIdentityCredential = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PostyFoxDevMode"))
            };
            clientBuilder.UseCredential(new DefaultAzureCredential(defaultCredentialOptions));
        });

    })
    .Build();

host.Run();
