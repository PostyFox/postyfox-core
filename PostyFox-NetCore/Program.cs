using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Azure;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddAzureClients(clientBuilder =>
        {
            // Register clients for each service
#pragma warning disable CS8604
            clientBuilder.AddTableServiceClient(new Uri(Environment.GetEnvironmentVariable("ConfigTable"))).WithName("ConfigTable");
#pragma warning restore CS8604
            clientBuilder.UseCredential(new DefaultAzureCredential());
        });
    })
    .Build();

host.Run();
