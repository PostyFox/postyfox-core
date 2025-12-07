using System;
using System.Net;
using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using PostyFox_DataLayer;
using PostyFox_DataLayer.TableEntities;
using PostyFox_NetCore.Helpers;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace PostyFox_NetCore
{
    public class Profile
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;

        public Profile(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            _logger = loggerFactory.CreateLogger<Profile>();
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
                _configTable.CreateTableIfNotExists("UserProfilesAPIKeys");
                var client = _configTable.GetTableClient("UserProfilesAPIKeys");
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

        [OpenApiOperation(tags: ["profile"], Summary = "Gets the users API tokens", Description = "Returns a (truncated) list of the users' configured API tokens", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<ProfileAPIKeyDTO>), Summary = "List of API Token", Description = "Truncated list of API token, note only the first six characters of the token returned")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [Function("Profile_GetAPITokens")]
        public HttpResponseData GetAPITokens([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("UserProfilesAPIKeys");
                var client = _configTable.GetTableClient("UserProfilesAPIKeys");
                string userId = AuthHelper.GetAuthId(req);

                List<ProfileAPIKeyDTO> ls = new();
                var query = client.Query<ProfileAPIKeyTableEntity>(x => x.PartitionKey == userId);
                foreach (var apiKey in query.AsEnumerable())
                {
                    ProfileAPIKeyDTO profileAPIKeyDTO = new()
                    {
                        APIKey = apiKey.APIKey,
                        UserID = userId,
                        ID = apiKey.RowKey
                    };
                    ls.Add(profileAPIKeyDTO);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                var valueTask = response.WriteAsJsonAsync(ls);
                valueTask.AsTask().GetAwaiter().GetResult();
                return response;
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                return response;
            }
        }

        [OpenApiOperation(tags: ["profile"], Summary = "Delete an API token", Description = "Delete an API token from a user profile", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Summary = "The result of the delete operation", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ProfileAPIKeyDTO), Required = true)]
        [Function("Profile_DeleteAPIToken")]
        public HttpResponseData DeleteUserService([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("UserProfilesAPIKeys");
                var client = _configTable.GetTableClient("UserProfilesAPIKeys");
                string userId = AuthHelper.GetAuthId(req);

                string requestBody = new StreamReader(req.Body).ReadToEnd();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                if (data != null)
                {
                    client.DeleteEntity(userId, data.ID); // Note that we ignore anything passed in on the token itself, only validating the userid and id valid of the token itself.
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
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
