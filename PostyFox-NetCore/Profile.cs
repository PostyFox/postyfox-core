using System;
using System.Net;
using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using PostyFox_NetCore.Helpers;

namespace PostyFox_NetCore
{
    public class Profile
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;

        public Profile(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            _logger = loggerFactory.CreateLogger<Services>();
            _configTable = clientFactory.CreateClient("ConfigTable");
        }


        [Function("Profile_Ping")]
        public HttpResponseData Ping([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var valueTask = response.WriteAsJsonAsync(req.Headers.ToString());
            valueTask.AsTask().GetAwaiter().GetResult();
            return response;
        }

        [OpenApiOperation(tags: ["profile"], Summary = "Generates an API Token", Description = "Generates an automatic API token for the current user, and returns it.", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProfileAPIKeyDTO), Summary = "API Token", Description = "API token")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [Function("Profile_GenerateAPIToken")]
        public HttpResponseData GenerateAPIToken([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("UserProfiles_APIKeys");
                var client = _configTable.GetTableClient("UserProfiles_APIKeys");
                string userId = AuthHelper.GetAuthId(req);

                Random random = new Random();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
                string key =  new(Enumerable.Repeat(chars, 40).Select(s => s[random.Next(s.Length)]).ToArray());

                ProfileAPIKeyTableEntity profileAPIKeyTableEntity = new()
                {
                    APIKey = key,
                    PartitionKey = userId,
                    RowKey = Guid.NewGuid().ToString().Replace("-", "")
                };
                client.AddEntity(profileAPIKeyTableEntity);

                ProfileAPIKeyDTO profileAPIKeyDTO = new()
                {
                    APIKey = profileAPIKeyTableEntity.APIKey,
                    UserID = userId,
                    ID = profileAPIKeyTableEntity.RowKey
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                var valueTask = response.WriteAsJsonAsync(profileAPIKeyDTO);
                valueTask.AsTask().GetAwaiter().GetResult();
                return response;
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                return response;
            }
        }

    }
}
