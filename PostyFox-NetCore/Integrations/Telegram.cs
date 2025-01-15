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
using PostyFox_NetCore.Helpers;
using TL;
using PostyFox_DataLayer;
using static PostyFox_NetCore.Services;
using System;

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
            _logger = loggerFactory.CreateLogger<Telegram>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
            _secretStore = secretClientFactory.CreateClient("SecretStore");

            // Load the configuration for Telegram from KeyVault
            apiId = int.Parse(_secretStore.GetSecret("TelegramApiID").Value.Value);
            apiHash = _secretStore.GetSecret("TelegramApiHash").Value.Value;
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
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ServiceRequest), Required = true)]
        [Function("Telegram_IsAuthenticated")]
        public HttpResponseData IsAuthenticated([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                string userId = AuthHelper.GetAuthId(req);
                _configTable.CreateTableIfNotExists("ConfigTable");
                var client = _configTable.GetTableClient("ConfigTable");

                string requestBody = new StreamReader(req.Body).ReadToEnd();
                ServiceRequest postBody = JsonConvert.DeserializeObject<ServiceRequest>(requestBody);
                string id = postBody.ID;

                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == userId && x.RowKey == id);
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

                        WTelegram.Client telegramClient = StaticState.GetTelegramClient(apiId, apiHash, userId, _blobStorageAccount);
                        var response = req.CreateResponse(HttpStatusCode.OK);
                        ValueTask valueTask;
                        if (telegramClient != null && telegramClient.UserId != 0)
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
                    else
                    {
                        var response = req.CreateResponse(HttpStatusCode.NotFound); // No configuration saved
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

        public class LoginParameters
        {
            /// <summary>
            /// The value that has been requested by the Telegram API
            /// </summary>
            [OpenApiPropertyAttribute(Description = "The value that has been requested by the Telegram API", Nullable = true)]
            public string? Value { get; set; }
            public string ID { get; set; }
        }

        public class LoginResponse
        {
            /// <summary>
            /// The current stage / status of the Login process
            /// </summary>
            [OpenApiPropertyAttribute(Description = "The current stage / status of the Login process")]
            public string Status { get; set; }
            /// <summary>
            /// The field name that should be used when you submit the JSON back
            /// </summary>
            [OpenApiPropertyAttribute(Description = "The field name that should be used when you submit the JSON back")]
            public string Input { get; set; }
            /// <summary>
            /// The Label that should be shown to the user for the required data
            /// </summary>
            [OpenApiPropertyAttribute(Description = "The Label that should be shown to the user for the required data", Nullable = true)]
            public string? Label { get; set; }
        }

        public class RequestParam
        {
            public string ID { get; set; }
        }

        [OpenApiOperation(tags: ["telegram"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(LoginResponse), Summary = "Returns with details for authentication flow", Description = "Returns with a JSON object detailing the Value required to proceed with authentication; submit via POST as a JSON body to continue.")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Summary = "No configuration found", Description = "No configuration stored for the user for the Telegram service")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LoginParameters), Required = false)]
        [Function("Telegram_DoLogin")]
        public HttpResponseData DoLogin([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("ConfigTable");
                var client = _configTable.GetTableClient("ConfigTable");
                string userId = AuthHelper.GetAuthId(req);
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                LoginParameters postBody = JsonConvert.DeserializeObject<LoginParameters>(requestBody);
                string id = postBody.ID;
                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == userId && x.RowKey == id);

                // Trigger the login flow, and see if we need more information - pass this back to client in response.
                ServiceTableEntity? entity = query.FirstOrDefault();
                if (entity != null && entity.Configuration != null && entity.ServiceID == "Telegram")
                {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    dynamic serviceConfig = JsonConvert.DeserializeObject(entity.Configuration);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                    string loginPayload = serviceConfig.PhoneNumber;
                    if (postBody.Value != null)
                    {
                        loginPayload = postBody.Value;
                    }

                    WTelegram.Client telegramClient = StaticState.GetTelegramClient(apiId, apiHash, userId, _blobStorageAccount);
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

        [OpenApiOperation(tags: ["telegram"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Dictionary<long, string>), Summary = "Returns the users accessible chats and channels", Description = "Returns with a JSON object detailing the users accessible chats and channels which the platform can post into")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Summary = "No configuration found", Description = "No configuration stored for the user for the Telegram service")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.MethodNotAllowed, contentType: "application/json", bodyType: typeof(KeyValuePair), Summary = "Service not authenticated", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RequestParam), Required = false)]
        [Function("Telegram_GetChannelsAndChats")]
        public HttpResponseData GetChannelsAndChats([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            HttpResponseData response;
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                var client = _configTable.GetTableClient("ConfigTable");
                string userId = AuthHelper.GetAuthId(req);
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                RequestParam postBody = JsonConvert.DeserializeObject<RequestParam>(requestBody);
                string id = postBody.ID;
                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == userId && x.RowKey == id);

                // Trigger the login flow, and see if we need more information - pass this back to client in response.
                ServiceTableEntity? entity = query.FirstOrDefault();
                if (entity != null && entity.Configuration != null && entity.ServiceID == "Telegram")
                {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    dynamic serviceConfig = JsonConvert.DeserializeObject(entity.Configuration);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                    WTelegram.Client telegramClient = StaticState.GetTelegramClient(apiId, apiHash, userId, _blobStorageAccount);
                    if (telegramClient.User != null)
                    {
                        // We are logged in all authed, so should be good to grab a list of the channels & chats we can post to 
                        Dictionary<long, string> chats = new Dictionary<long, string>();
                        var chatsResult = telegramClient.Messages_GetAllChats();
                        chatsResult.Wait();
                        foreach (var chat in chatsResult.Result.chats)
                        {
                            chats.Add(chat.Value.ID, chat.Value.Title);
                        }

                        response = req.CreateResponse(HttpStatusCode.OK);
                        response.WriteAsJsonAsync(chats);
                        return response;
                    } else {
                        response = req.CreateResponse(HttpStatusCode.MethodNotAllowed);
                        response.WriteString("{\"error\":\"NotLoggedIn\"}");
                        return response;

                    }
                } else {
                    response = req.CreateResponse(HttpStatusCode.NotFound); // No configuration saved
                    return response;
                }
            }
            else
            {
                response = req.CreateResponse(HttpStatusCode.Unauthorized);
                return response;
            }
        }
    }
}
