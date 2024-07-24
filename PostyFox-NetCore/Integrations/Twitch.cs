using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Optional.Unsafe;
using PostyFox_DataLayer.TableEntities;
using PostyFox_NetCore.Helpers;
using System.Net;
using System.Text.Json;
using TL;
using Twitch.Net.Api.Client;
using Twitch.Net.EventSub;

namespace PostyFox_NetCore.Integrations
{

    public class Twitch
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly IApiClient _apiClient;
        private readonly IEventSubService _eventSubService;
        private readonly EventSubBuilder _eventSubBuilder;

        public Twitch(ILoggerFactory loggerFactory, IApiClient apiClient, IEventSubService eventSubService, EventSubBuilder eventSubBuilder, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            _logger = loggerFactory.CreateLogger<Twitch>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _apiClient = apiClient;
            _eventSubService = eventSubService;
            _eventSubBuilder = eventSubBuilder;
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
                            RowKey = user.Id,
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
                        _configTable.CreateTableIfNotExists("ExternalInterests");
                        var externalInterestsClient = _configTable.GetTableClient("ExternalInterests");
                        // a. Check for an existing entry
                        // - Pull the config and add entry
                        var rec = externalInterestsClient.GetEntityIfExists<ExternalInterestsTableEntity>("Twitch", user.Id);

                        if (rec.HasValue)
                        {
                            List<string> conf = JsonSerializer.Deserialize<List<string>>(rec.Value.Configuration);
                            if (!conf.Contains(userId))
                            {
                                conf.Add(userId);
                                rec.Value.Configuration = JsonSerializer.Serialize(conf);
                                externalInterestsClient.UpsertEntity(rec.Value);
                            }
                        } 
                        else
                        {
                            List<string> conf = new List<string>();
                            conf.Add(userId);
                            // b. No entry, add a new entry
                            ExternalInterestsTableEntity externalInterestsTableEntity = new()
                            {
                                PartitionKey = "Twitch",
                                RowKey = user.Id,
                                Configuration = JsonSerializer.Serialize(conf)
                            };
                            externalInterestsClient.UpsertEntity(externalInterestsTableEntity);
                        }

                        response = req.CreateResponse(HttpStatusCode.OK);
                        // Kick off a call to twitch - EventSubTypes.StreamOnline, EventSubTypes.StreamOffline
                        var twitchModel = _eventSubBuilder.Build(EventSubTypes.StreamOnline, "");
                        if (twitchModel.HasValue)
                        {
                            var twitchResult = await _eventSubService.Subscribe(twitchModel.ValueOrFailure());
                            
                        }

                        
                        return response;
                    }
                }
            }
            response = req.CreateResponse(HttpStatusCode.Unauthorized);
            return response;
        }
    }
}
