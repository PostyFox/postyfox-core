using System.Net;
using Azure;
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
using PostyFox_NetCore;
using PostyFox_NetCore.Helpers;
using TL;

namespace PostyFox_NetCore.Integrations
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
            _logger = loggerFactory.CreateLogger<Services>();
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

            _logger = loggerFactory.CreateLogger<Services>();
            _configTable = clientFactory.CreateClient("ConfigTable");

            apiId = int.Parse(Environment.GetEnvironmentVariable("TelegramApiID"));
            apiHash = Environment.GetEnvironmentVariable("TelegramApiHash");
        }

        [Function("Telegram_Ping")]
        public HttpResponseData Ping([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var valueTask = response.WriteAsJsonAsync(req.Headers.ToString());
            valueTask.AsTask().GetAwaiter().GetResult();
            return response;
        }

        [OpenApiOperation(tags: ["telegram"], Summary = "Identifies if the user is authenticated with Telegram", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(bool), Summary = "true if a valid session is held", Description = "If no valid session, call authentication flow")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Summary = "No configuration found", Description = "No configuration stored for the user for the Telegram service")]
        [Function("Telegram_IsAuthenticated")]
        public HttpResponseData IsAuthenticated([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                string userId = AuthHelper.GetAuthId(req);
                _configTable.CreateTableIfNotExists("ConfigTable");
                var client = _configTable.GetTableClient("ConfigTable");

                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == userId && x.RowKey == "Telegram");

                // Trigger the login flow, and see if we need more information - pass this back to client in response.
                ServiceTableEntity? entity = query.FirstOrDefault();
                if (entity != null)
                {
                    dynamic serviceConfig = JsonConvert.DeserializeObject(entity.Configuration);
                    string requestBody = new StreamReader(req.Body).ReadToEnd();
                    string loginPayload = serviceConfig.PhoneNumber;

                    TelegramStore store = new TelegramStore(userId, _blobStorageAccount);
                    using (WTelegram.Client telegramClient = new WTelegram.Client((val) =>
                    {
                        if (val == "api_id") return apiId.ToString();
                        if (val == "api_hash") return apiHash;
                        if (val == "phone_number") return loginPayload;
                        return null;
                    }, store))
                    {
                        var response = req.CreateResponse(HttpStatusCode.OK);
                        ValueTask valueTask;
                        if (telegramClient.UserId != 0)
                        {
                            var t = telegramClient.Login(loginPayload);
                            t.Wait();
                            if (t.Result == null)
                            {
                                valueTask = response.WriteAsJsonAsync(true);
                                valueTask.AsTask().GetAwaiter().GetResult();
                            }
                        }
                        else
                        {
                            valueTask = response.WriteAsJsonAsync(false);
                            valueTask.AsTask().GetAwaiter().GetResult();
                        }
                        return response;
                    }
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.NotFound); // No configuration saved
                    return response;
                }
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                return response;
            }
        }

        [OpenApiOperation(tags: ["telegram"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Summary = "Returns with details for authentication flow", Description = "Returns with a JSON object detailing the Value required to proceed with authentication; submit via POST as a JSON body to continue.")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Summary = "No configuration found", Description = "No configuration stored for the user for the Telegram service")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(string), Required = false)]
        [Function("Telegram_DoLogin")]
        public HttpResponseData DoLogin([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("ConfigTable");
                var client = _configTable.GetTableClient("ConfigTable");
                string userId = AuthHelper.GetAuthId(req);

                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == userId && x.RowKey == "Telegram");

                // Trigger the login flow, and see if we need more information - pass this back to client in response.
                ServiceTableEntity? entity = query.FirstOrDefault();
                if (entity != null)
                {
                    dynamic serviceConfig = JsonConvert.DeserializeObject(entity.Configuration);
                    string requestBody = new StreamReader(req.Body).ReadToEnd();
                    string loginPayload = serviceConfig.PhoneNumber;
                    if (!string.IsNullOrEmpty(requestBody)) {
                        dynamic postBody = JsonConvert.DeserializeObject(requestBody);
                        if (postBody != null)
                        {
                            if (postBody.Value != null)
                            {
                                loginPayload = postBody.Value;
                            }
                        }
                    }

                    WTelegram.Client telegramClient = StaticState.GetTelegramClient(apiId, apiHash, userId, _blobStorageAccount);
                    telegramClient.OnOther += TelegramClient_OnOther;
                    var t = telegramClient.Login(loginPayload);
                    t.Wait();
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    switch (t.Result) // returns which config is needed to continue login
                    {
                        case "verification_code":
                            response.WriteAsJsonAsync("{\"Status\":\"InProgress\", \"Input\":\"Value\", \"Label\":\"Verification Code\"}").GetAwaiter().GetResult();
                            break;
                        case "password":
                            response.WriteAsJsonAsync("{\"Status\":\"InProgress\", \"Input\":\"Value\", \"Label\":\"2FA Password\"}").GetAwaiter().GetResult();
                            break;
                        default:
                            response.WriteAsJsonAsync("{\"Status\":\"Complete\", \"LoggedIn\": " + (telegramClient.User != null) + " }").GetAwaiter().GetResult();
                            StaticState.DisposeTelegramClient(userId);
                            break;
                    }
                    return response;
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.NotFound); // No configuration saved
                    return response;
                }
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                return response;
            }
        }

        private Task TelegramClient_OnOther(IObject arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }
    }
}