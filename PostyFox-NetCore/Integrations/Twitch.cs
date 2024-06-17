using Azure.Data.Tables;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using PostyFox_DataLayer.TableEntities;
using PostyFox_NetCore.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using TL;
using Twitch.Net.Api.Client;
using Twitch.Net.EventSub;
using static PostyFox_NetCore.Services;
//using TwitchLiveNotifications.Helpers;
//using TwitchLiveNotifications.Models;

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
            _apiClient = apiClient;
            _configTable = clientFactory.CreateClient("ConfigTable");
        }

        public class Twitch_RegisterSub
        {
            public string channelName { get; set; }
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
                        ServiceTableEntity twitchSubTableEntity = new ServiceTableEntity()
                        {
                            ServiceName = user.DisplayName,
                            Configuration = user.Id,
                            PartitionKey = userId,
                            RowKey="Tw-"+userId+"-"+user.Id,
                            ServiceID = "TwitchSubscription"
                        };

                        // Commit to the table storage (Upsert) 
                        _configTable.CreateTableIfNotExists("ConfigTable");
                        var client = _configTable.GetTableClient("ConfigTable");
                        client.UpsertEntity(twitchSubTableEntity);

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