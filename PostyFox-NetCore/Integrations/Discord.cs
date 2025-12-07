using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using System.Net;
using Neillans.Adapters.Secrets.Core;

namespace PostyFox_NetCore.Integrations
{
    public class Discord
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly ISecretsProvider? _secretsProvider;
        private readonly BlobServiceClient _blobStorageAccount;

        public Discord(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, ISecretsProvider? secretsProvider, IAzureClientFactory<BlobServiceClient> blobClientFactory)
        {
            _logger = loggerFactory.CreateLogger<Discord>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
            _secretsProvider = secretsProvider;
        }

        [Function("Discord_Ping")]
        public HttpResponseData Ping([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var valueTask = response.WriteAsJsonAsync(req.Headers.ToString());
            valueTask.AsTask().GetAwaiter().GetResult();
            return response;
        }

        // This is a placeholder service integration point, as we may want to build a bot integration.

        // Initially, however, we will support Webhook calls only.
    }
}
