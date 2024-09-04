using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using System.Net;
using Twitch.Net.EventSub;
using Twitch.Net.EventSub.Models;

namespace PostyFox_Posting
{
    public class Twitch
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly SecretClient? _secretStore;
        private readonly BlobServiceClient _blobStorageAccount;
        private readonly IEventSubService2 _eventSubService;

        public Twitch(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, IAzureClientFactory<SecretClient> secretClientFactory, IAzureClientFactory<BlobServiceClient> blobClientFactory, IEventSubService2 eventSubService)
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


        [Function("Twitch_SubscriptionCallBack")]
        public async Task<HttpResponseData> Callback([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req,
            FunctionContext executionContext)
        {
            string? messageType = req.Headers.GetValues(EventSubHeaderConst.MessageType).FirstOrDefault();
            string? messageId = req.Headers.GetValues(EventSubHeaderConst.MessageId).FirstOrDefault();
            string[] requestList = Array.Empty<string>();

            var response = req.CreateResponse(HttpStatusCode.OK);

            // For notification events, we need to keep track of duplicate requests.
            if (messageType != null && messageId != null && messageType.ToLower() == "notification")
            {
                _configTable.CreateTableIfNotExists("TwitchRequests");
                var body = await (new StreamReader(req.Body).ReadToEndAsync());

                // Check for duplicated requests (Twitch can sometimes send multiple, so check ID and drop dupes)
                if (TwitchHelpers.RequestLogger.LogRequest("", messageId, _configTable.GetTableClient("TwitchRequests")))
                {
                    // If we get here it wasnt a duplicate, so get on with raising it
                    _logger.LogInformation("Twitch Callback received {0}: {1}", messageId, body);
                    SubscribeCallbackResponse result = _eventSubService.Handle(req.Headers, body);

                    // TODO: LOAD TEMPLATE DATA
                    // TODO: ADD TO THE QUEUE
                    
                    string responseBody = result.CallBack.Challenge ?? "";
                    HttpResponseData resp = req.CreateResponse(result.StatusCode);
                    resp.Headers.Add("content-type", "text/plain");
                    resp.WriteString(responseBody);
                    return resp;
                }
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
