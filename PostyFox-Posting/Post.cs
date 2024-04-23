using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PostyFox_DataLayer.TableEntities;
using PostyFox_DataLayer;
using System.Net;
using Azure.Data.Tables;
using Microsoft.Extensions.Azure;

namespace PostyFox_Posting
{
    public class Post
    {
        private readonly ILogger<Post> _logger;
        private readonly TableServiceClient _configTable;

        public Post(ILogger<Post> logger, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            _logger = logger;
            _configTable = clientFactory.CreateClient("ConfigTable");
        }



        public class PostParameters
        {
            public PostyFox_DataLayer.ProfileAPIKeyDTO APIKey { get; set; }

            public List<string> TargetPlatforms { get; set; }

            public List<string> Media { get; set; } // Will probably need to do this using a seperate upload and resource ID reference model ? 

            public string Title { get; set; }

            public string Description { get; set; }

            public string HTMLDescription { get; set; }

            public List<string> Tags { get; set; }

            public DateTime? PostAt { get; set; }
        }

        public class PostResponse
        { 
            public string PostId { get; set; }
            public string Status { get; set; }
            public string MediaSavedUri { get; set; }
        }

        [OpenApiOperation(tags: ["post"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PostResponse), Summary = "Returns with details of the post", Description = "Returns with details of the post")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PostParameters), Required = true)]
        [Function("Post")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            // Confirm that have a API Key

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            PostParameters para = data as PostParameters;
            if (para != null)
            {
                // Lookup against the keys, check that we have a valid key before we continue
                _logger.LogDebug("Validating API Key data", para);

                _configTable.CreateTableIfNotExists("UserProfiles_APIKeys");

                var client = _configTable.GetTableClient("UserProfiles_APIKeys");
                var query = client.Query<ProfileAPIKeyTableEntity>(x => x.PartitionKey == para.APIKey.UserID && x.RowKey == para.APIKey.ID);
                var valid = query.FirstOrDefault();
                if (valid != null)
                {
                    // Key is valid for user

                    // Extract out and save the post data to storage
                    foreach (var targetPlatform in para.TargetPlatforms)
                    {

                        // Save the post to queue - Parse out the targets and convert each to a QueueEntity and save to queue
                        // Schedule as required
                    }
                } 
                else
                {
                    _logger.LogDebug("API Key is invalid or could not be verified");
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
    }
}
