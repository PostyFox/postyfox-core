using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PostyFox_DataLayer;
using PostyFox_DataLayer.TableEntities;
using PostyFox_NetCore.Helpers;
using System.Net;
using static PostyFox_NetCore.Integrations.Telegram;
using static PostyFox_NetCore.Services;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

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

        [OpenApiOperation(tags: ["postingtemplates"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<PostingTemplateDTO>), Summary = "Returns a list of Posting Templates", Description = "Returns a list of the current users posting templates")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [Function("PostingTemplates_GetTemplates")]
        public HttpResponseData GetTemplates([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                string userId = AuthHelper.GetAuthId(req);

                _configTable.CreateTableIfNotExists("PostingTemplates");

                var postingTemplateTable = _configTable.GetTableClient("PostingTemplates");
                var usersPostingTemplates = postingTemplateTable.Query<PostingTemplateTableEntity>(s => s.PartitionKey == userId);

                // Parse and and convert to DTO - return. 
                List<PostingTemplateDTO> postingTemplates = new();
                foreach (var postingTemplate in usersPostingTemplates)
                {
                    postingTemplates.Add(new PostingTemplateDTO
                    {
                        ID = postingTemplate.RowKey,
                        Title = postingTemplate.Title,
                        MarkdownBody = postingTemplate.MarkdownBody
                    });
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                var valueTask = response.WriteAsJsonAsync(postingTemplates);
                valueTask.AsTask().GetAwaiter().GetResult();
                return response;
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                return response;
            }
        }

        // TODO: Delete template


        [OpenApiOperation(tags: ["postingtemplates"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Summary = "Returns status of the upsert", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PostingTemplateDTO), Required = true)]
        [Function("PostingTemplates_SaveTemplate")]
        public HttpResponseData SaveTemplate([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                string userId = AuthHelper.GetAuthId(req);

                _configTable.CreateTableIfNotExists("PostingTemplates");

                string requestBody = new StreamReader(req.Body).ReadToEnd();
                PostingTemplateDTO postBody = JsonConvert.DeserializeObject<PostingTemplateDTO>(requestBody);

                var postingTemplateTable = _configTable.GetTableClient("PostingTemplates");

                if (string.IsNullOrEmpty(postBody.ID))
                {
                    postBody.ID = Guid.NewGuid().ToString();
                }

                var templateResponse = postingTemplateTable.GetEntityIfExists<PostingTemplateTableEntity>(userId, postBody.ID);
                PostingTemplateTableEntity template;
                if (!templateResponse.HasValue)
                {
                    template = new PostingTemplateTableEntity
                    {
                        PartitionKey = userId,
                        RowKey = postBody.ID,
                    };
                }
                else
                {
                    template = templateResponse.Value;
                }

                template.Title = postBody.Title;
                template.MarkdownBody = postBody.MarkdownBody;

                postingTemplateTable.UpsertEntity(template);

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
