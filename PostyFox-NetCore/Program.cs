using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureServices(services =>
    {
        services.AddAzureClients(clientBuilder =>
        {
            // Register clients for each service
#pragma warning disable CS8604
            clientBuilder.AddTableServiceClient(new Uri(Environment.GetEnvironmentVariable("ConfigTable"))).WithName("ConfigTable");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SecretStore")))
            {
                clientBuilder.AddSecretClient(new Uri(Environment.GetEnvironmentVariable("SecretStore"))).WithName("SecretStore");
            }
#pragma warning restore CS8604
            clientBuilder.UseCredential(new DefaultAzureCredential());
        });
        
    })
    .Build();

host.Run();
