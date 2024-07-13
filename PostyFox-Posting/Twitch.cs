using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using System.Net;
using Twitch.Net.EventSub;

namespace PostyFox_Posting
{
    public class Twitch
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly SecretClient? _secretStore;
        private readonly BlobServiceClient _blobStorageAccount;
        private readonly IEventSubService _eventSubService;

        public Twitch(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, IAzureClientFactory<SecretClient> secretClientFactory, IAzureClientFactory<BlobServiceClient> blobClientFactory, IEventSubService eventSubService)
        {
            _logger = loggerFactory.CreateLogger<Twitch>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
            _secretStore = secretClientFactory.CreateClient("SecretStore");
            _eventSubService = eventSubService;

            //// Load the configuration for Telegram from KeyVault
            //apiId = int.Parse(_secretStore.GetSecret("TelegramApiID").Value.Value);
            //apiHash = _secretStore.GetSecret("TelegramApiHash").Value.Value;
        }


        [Function("Twitch_Callback")]
        public async Task<HttpResponseData> Callback([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            // Extract the Twitch headers
            var messageType = req.Headers.GetValues(EventSubHeaderConst.MessageType).FirstOrDefault();
            var messageId = req.Headers.GetValues(EventSubHeaderConst.MessageId).FirstOrDefault();

            var response = req.CreateResponse(HttpStatusCode.OK);

            if (messageType != null && messageId != null && messageType.ToLower() == "notification")
            {
                _configTable.CreateTableIfNotExists("TwitchRequests");
                var body = await (new StreamReader(req.Body).ReadToEndAsync());

                //        // Check for duplicated requests (Twitch can sometimes send multiple, so check ID and drop dupes)
                //        if (TwitchHelpers.RequestLogger.LogRequest("", messageId, _configTable.GetTableClient("TwitchRequests")))
                //        {
                //            // Log
                //            _logger.LogInformation("Twitch Callback received {0}: {1}", messageId, body);

                //            // Decode and validate
                //            _eventSubService.Handle(req)

                //            // Queue up whatever we need to go notify

                //        }
            }

            // Send back OK (otherwise Twitch has a hissy fit and kills our service subscriptions off)

            return response;
        }

    }
}
