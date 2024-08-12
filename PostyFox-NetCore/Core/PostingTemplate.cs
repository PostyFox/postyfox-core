using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PostyFox_DataLayer.TableEntities;
using System.Net;
using static PostyFox_NetCore.Integrations.Telegram;

namespace PostyFox_NetCore
{
    public class PostingTemplate
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;

        public PostingTemplate(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            _logger = loggerFactory.CreateLogger<PostingTemplate>();
            _configTable = clientFactory.CreateClient("ConfigTable");
        }


        [Function("PostingTemplate_Ping")]
        public HttpResponseData Ping([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var valueTask = response.WriteAsJsonAsync(req.Headers.ToString());
            valueTask.AsTask().GetAwaiter().GetResult();
            return response;
        }

        public class PostingTemplateParameters 
        {
            public string UserID = "";
        }



        //[OpenApiOperation(tags: ["telegram"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(LoginResponse), Summary = "Returns with details for authentication flow", Description = "Returns with a JSON object detailing the Value required to proceed with authentication; submit via POST as a JSON body to continue.")]
        //[OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Summary = "No configuration found", Description = "No configuration stored for the user for the Telegram service")]
        //[OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        //
        //
        //public HttpResponseData DoLogin([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        [Function("PostingTemplates_GetTemplates")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PostingTemplateParameters), Required = true)]
        public HttpResponseData GetTemplates([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _configTable.CreateTableIfNotExists("PostingTemplates");

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            PostingTemplateParameters data = JsonConvert.DeserializeObject<PostingTemplateParameters>(requestBody);

            var postingTemplateTable = _configTable.GetTableClient("PostingTemplates");
            var usersPostingTemplates = postingTemplateTable.Query<PostingTemplateTableEntity>(s => s.PartitionKey==data.UserID);

            // Parse and and convert to DTO - return. 


            // Pull a list from the Storage Account Table - do we need to partition this based on the user ID ? 
            // Probably. Probably meed to use a template filter too . Or do we split down by type ? 

            var response = req.CreateResponse(HttpStatusCode.OK);
            var valueTask = response.WriteAsJsonAsync(req.Headers.ToString());
            valueTask.AsTask().GetAwaiter().GetResult();
            return response;
        }

        public HttpResponseData GetTemplateDetail()
        {
            _configTable.CreateTableIfNotExists("PostingTemplates");
        }

        public HttpResponseData UpsertTemplate()
        {
            _configTable.CreateTableIfNotExists("PostingTemplates");
        }

        //[OpenApiOperation(tags: ["profile"], Summary = "Generates an API Token", Description = "Generates an automatic API token for the current user, and returns it.", Visibility = OpenApiVisibilityType.Important)]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProfileAPIKeyDTO), Summary = "API Token", Description = "API token")]
        //[OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        //[Function("Profile_GenerateAPIToken")]
        //public HttpResponseData GenerateAPIToken([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        //{
        //    if (AuthHelper.ValidateAuth(req, _logger))
        //    {
        //        _configTable.CreateTableIfNotExists("UserProfiles_APIKeys");
        //        var client = _configTable.GetTableClient("UserProfiles_APIKeys");
        //        string userId = AuthHelper.GetAuthId(req);

        //        Random random = new Random();
        //        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
        //        string key =  new(Enumerable.Repeat(chars, 40).Select(s => s[random.Next(s.Length)]).ToArray());

        //        ProfileAPIKeyTableEntity profileAPIKeyTableEntity = new()
        //        {
        //            APIKey = key,
        //            PartitionKey = userId,
        //            RowKey = Guid.NewGuid().ToString().Replace("-", "")
        //        };
        //        client.AddEntity(profileAPIKeyTableEntity);

        //        ProfileAPIKeyDTO profileAPIKeyDTO = new()
        //        {
        //            APIKey = profileAPIKeyTableEntity.APIKey,
        //            UserID = userId,
        //            ID = profileAPIKeyTableEntity.RowKey
        //        };

        //        var response = req.CreateResponse(HttpStatusCode.OK);
        //        var valueTask = response.WriteAsJsonAsync(profileAPIKeyDTO);
        //        valueTask.AsTask().GetAwaiter().GetResult();
        //        return response;
        //    }
        //    else
        //    {
        //        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        //        return response;
        //    }
        //}

    }
}
