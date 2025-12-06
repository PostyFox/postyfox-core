using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using PostyFox_DataLayer.TableEntities;
using TL;
using PostyFox_DataLayer;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Neillans.Adapters.Secrets.Core;

namespace PostyFox_Posting
{
    public class Telegram
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly ISecretsProvider? _secretsProvider;
        private readonly BlobServiceClient _blobStorageAccount;
        private int apiId = 0;
        private string apiHash = string.Empty;

        public Telegram(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, ISecretsProvider? secretsProvider, IAzureClientFactory<BlobServiceClient> blobClientFactory)
        {
            _logger = loggerFactory.CreateLogger<Telegram>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
            _secretsProvider = secretsProvider;

            if (_secretsProvider is not null)
            {
                var id = _secretsProvider.GetSecretAsync("TelegramApiID").GetAwaiter().GetResult();
                var hash = _secretsProvider.GetSecretAsync("TelegramApiHash").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(id)) apiId = int.Parse(id);
                apiHash = hash ?? string.Empty;
            }
            else
            {
#pragma warning disable CS8604 // Possible null reference argument.
                apiId = int.Parse(Environment.GetEnvironmentVariable("TelegramApiID"));
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning disable CS8601 // Possible null reference assignment.
                apiHash = Environment.GetEnvironmentVariable("TelegramApiHash");
#pragma warning restore CS8601 // Possible null reference assignment.
            }
        }

        public Telegram(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            // This constructor will be used when there is no secrets provider provided by dependency injection - i.e. we are running locally.

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


        public class TelegramParameters
        {
            public string UserId { get; set; }

            public PostyFox_DataLayer.ProfileAPIKeyDTO APIKey { get; set; }

            [OpenApiProperty(Description = "Service ID")]
            public string Id { get; set; }
        }

        public class TelegramChatResponse
        {
            public Dictionary<long, string> ChatList { get; set; }
        }

        [OpenApiOperation(tags: ["telegram"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(TelegramParameters), Required = false)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TelegramChatResponse), Summary = "Details of the accessible Telegram Chats", Description = "Details of the accessible Telegram Chats")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Summary = "No configuration found", Description = "No configuration stored for the user for the Telegram service")]
        [Function("Telegram_GetAccessibleChats")]
        public async Task<HttpResponseData> GetAccessibleChats([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _configTable.CreateTableIfNotExists("ConfigTable");
            var client = _configTable.GetTableClient("ConfigTable");

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            TelegramParameters postBody = JsonConvert.DeserializeObject<TelegramParameters>(requestBody);
            if (postBody != null)
            {
                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == postBody.UserId && x.RowKey == postBody.Id);
                // Trigger the login flow, and see if we need more information - pass this back to client in response.
                ServiceTableEntity? entity = query.FirstOrDefault();
                if (entity != null && entity.Configuration != null && entity.ServiceID == "Telegram")
                {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    dynamic serviceConfig = JsonConvert.DeserializeObject(entity.Configuration);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                    if (serviceConfig != null)
                    {
                        string loginPayload = serviceConfig.PhoneNumber;
                        //Get the state file for the user
                        TelegramStore store = new TelegramStore(postBody.UserId, _blobStorageAccount);

                        //Check valid session
                        using WTelegram.Client telegramClient = new((val) =>
                        {
                            if (val == "api_id") return apiId.ToString();
                            if (val == "api_hash") return apiHash;
                            if (val == "phone_number") return loginPayload;
                            return null;
                        }, store);

                        TelegramChatResponse telegramChatResponse = new()
                        {
                            ChatList = []
                        };

                        if (telegramClient.UserId != 0)
                        {
                            var t = telegramClient.Login(loginPayload);
                            t.Wait();
                            if (t.Result == null)
                            {

                                var chats = await telegramClient.Messages_GetAllChats();
                                foreach (var (id, chat) in chats.chats)
                                {
                                    if (chat.IsActive)
                                    {
                                        telegramChatResponse.ChatList.Add(id, chat.Title);
                                    }
                                }
                            }
                        }

                        var okResponse = req.CreateResponse(HttpStatusCode.OK);
                        var valueTask = okResponse.WriteAsJsonAsync(JsonConvert.SerializeObject(telegramChatResponse));
                        valueTask.AsTask().GetAwaiter().GetResult();
                        return okResponse;
                    }
                }
            }
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            return response;
        }

        private Task TelegramClient_OnOther(IObject arg)
        {
            return Task.CompletedTask;
        }
    }
}
