using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Twitch.Net.Api.Client;
using Twitch.Net.EventSub;
using TwitchLiveNotifications.Helpers;
using TwitchLiveNotifications.Models;

namespace PostyFox_Posting
{

    public class Twitch_RegisterSub
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;

        public Twitch_RegisterSub(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory,)
        {
            _logger = loggerFactory.CreateLogger<Twitch_RegisterSub>();
            _configTable = clientFactory.CreateClient("ConfigTable");
        }

        [Function("RegisterSubscription")]
        public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            HttpResponseData response;
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var configs = JsonSerializer.Deserialize<List<SubscriptionConfig>>(requestBody);
            foreach (var config in configs)
            {
                _logger.LogInformation("Resolving user: {user}", config.TwitchName);
                var users = await _apiClient.Helix.Users.GetUsersAsync(logins: new List<string>() { config.TwitchName });
                if (users.Successfully != 1)
                {
                    response = req.CreateResponse(HttpStatusCode.BadRequest);
                    response.Headers.Add("Content-Type", "text; charset=utf-8");
                    response.WriteString($"Invalid twitch user: {config.TwitchName}");
                    return response;
                }

                var user = users.Users.FirstOrDefault();
                if (user == null)
                {
                    // No user found
                    _logger.LogInformation("No user found for: {user} when attempting to register subscription", config.TwitchName);
                    continue;
                }

                config.TwitchId = user.Id;
                SubscriptionConfig.SetTwitchSubscriptionConfiguration(config, _configTable);

                TwitchSubscription subscription;
                foreach (var type in new string[] { EventSubTypes.StreamOnline, EventSubTypes.StreamOffline })
                {
                    subscription = new TwitchSubscription()
                    {
                        Type = type,
                        Value = config.TwitchId
                    };
                    var message = JsonSerializer.Serialize(subscription);
                    _logger.LogInformation("Posting message: {message}", message);
                    QueueHelpers.SendMessage(_logger, _queueClientService, Constants.QueueAddSubscription, message);
                }
            }
            response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Users registered.");

            return response;
        }
    }
}