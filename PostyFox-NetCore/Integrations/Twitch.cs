using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PostyFox_DataLayer.TableEntities;
using PostyFox_NetCore.Helpers;
using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Twitch.Net.Api.Client;
using Twitch.Net.EventSub;

namespace PostyFox_NetCore.Integrations
{

    public class Twitch
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly IApiClient _apiClient;

        public Twitch(ILoggerFactory loggerFactory, IApiClient apiClient, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            _logger = loggerFactory.CreateLogger<Twitch>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _apiClient = apiClient;
        }

        public class Twitch_RegisterSub
        {
            public string channelName { get; set; }
            public string channelId { get; set; }
            public string webhookPost { get; set; }
            public string postTemplate { get; set; }
            public int notifyFrequencyHrs { get; set; }
            public string targetPlatform { get; set; } 
        }

        [OpenApiOperation(tags: ["twitch"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Summary = "", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Summary = "No channel found", Description = "")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Twitch_RegisterSub), Required = true)]
        [Function("Twitch_RegisterSubscription")]
        public async Task<HttpResponseData> RegisterSubscription([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            HttpResponseData response;

            if (AuthHelper.ValidateAuth(req, _logger))
            {
                string userId = AuthHelper.GetAuthId(req);

                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var registerSub = JsonSerializer.Deserialize<Twitch_RegisterSub>(requestBody);

                if (registerSub != null)
                {
                    _logger.LogInformation("Resolving user: {user}", registerSub.channelName);
                    var users = await _apiClient.Helix.Users.GetUsersAsync(logins: [registerSub.channelName]);
                    if (users.Successfully != 1)
                    {
                        response = req.CreateResponse(HttpStatusCode.NotFound);
                        return response;
                    }

                    var user = users.Users.FirstOrDefault();
                    if (user == null)
                    {
                        // No user found
                        _logger.LogInformation("No user found for: {user} when attempting to register subscription", registerSub.channelName);
                    }
                    else
                    {
                        registerSub.channelId = user.Id;

                        // 1. Create entry in ExternalTrigger table - user configuration basically
                        ExternalTriggerTableEntity externalTriggerTableEntity = new()
                        {
                            RowKey = userId + "-" + user.Id,
                            PartitionKey = userId,
                            ExternalAccount = user.Id,
                            ExternalAccountType = "Twitch",
                            Template = registerSub.postTemplate,
                            TargetPlatform = registerSub.targetPlatform,
                            NotifyFrequencyHrs = registerSub.notifyFrequencyHrs
                        };

                        // Commit to the table storage (Upsert) 
                        _configTable.CreateTableIfNotExists("ExternalTriggers");
                        var client = _configTable.GetTableClient("ExternalTriggers");
                        client.UpsertEntity(externalTriggerTableEntity);

                        // 2. Create entry in ExternalInterests table - this is the map of triggers back to ExternalTrigger for incoming hooks
                        ExternalInterestsTableEntity externalInterestsTableEntity = new()
                        {
                            //                    [Description("External Service Type (i.e. Twitch)")]
                            //public string PartitionKey { get; set; }
                            //[Description("Twitch (etc) User ID")]
                            //public string RowKey { get; set; }
                            //public string Configuration { get; set; }

                        };

                        _configTable.CreateTableIfNotExists("ExternalInterests");
                        var externalInterestsClient = _configTable.GetTableClient("ExternalInterests");
                        externalInterestsClient.UpsertEntity(externalInterestsTableEntity);

                        // Kick off a call to twitch - EventSubTypes.StreamOnline, EventSubTypes.StreamOffline

                        response = req.CreateResponse(HttpStatusCode.OK);
                        return response;
                    }
                }
            }
            response = req.CreateResponse(HttpStatusCode.Unauthorized);
            return response;
        }
    }
}
