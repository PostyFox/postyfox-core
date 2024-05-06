using System.Net;
using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using PostyFox_DataLayer.TableEntities;
using TL;
using PostyFox_DataLayer;

namespace PostyFox_Posting
{
    public class Telegram
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly SecretClient? _secretStore;
        private readonly BlobServiceClient _blobStorageAccount;
        private int apiId = 0;
        private string apiHash = string.Empty;

        public Telegram(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, IAzureClientFactory<SecretClient> secretClientFactory, IAzureClientFactory<BlobServiceClient> blobClientFactory)
        {
            _logger = loggerFactory.CreateLogger<Telegram>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
            _secretStore = secretClientFactory.CreateClient("SecretStore");

            // Load the configuration for Telegram from KeyVault
            apiId = int.Parse(_secretStore.GetSecret("TelegramApiID").Value.Value);
            apiHash = _secretStore.GetSecret("TelegramApiHash").Value.Value;
        }

        public Telegram(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            // This constructor will be used when there is no secretStore provided by dependency injection - i.e. we are running locally.

            _logger = loggerFactory.CreateLogger<Telegram>();
            _configTable = clientFactory.CreateClient("ConfigTable");

#pragma warning disable CS8604 // Possible null reference argument.
            apiId = int.Parse(Environment.GetEnvironmentVariable("TelegramApiID"));
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning disable CS8601 // Possible null reference assignment.
            apiHash = Environment.GetEnvironmentVariable("TelegramApiHash");
#pragma warning restore CS8601 // Possible null reference assignment.
        }

        [Function("Telegram_Ping")]
        public HttpResponseData Ping([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var valueTask = response.WriteAsJsonAsync(req.Headers.ToString());
            valueTask.AsTask().GetAwaiter().GetResult();
            return response;
        }

        [Function("Telegram_GetAccessibleChats")]
        public HttpResponseData GetAccessibleChats([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            //Get the state file for the user
            TelegramStore store = new TelegramStore(userId, blobServiceClient);

            //Check valid session


            //Pull available chats

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private Task TelegramClient_OnOther(IObject arg)
        {
            return Task.CompletedTask;
        }
    }
}
