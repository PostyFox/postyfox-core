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
using Azure.Storage.Blobs;

namespace PostyFox_Posting
{
    public class Post
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly BlobServiceClient _blobStorageAccount;

        public Post(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, IAzureClientFactory<BlobServiceClient> blobClientFactory)
        {
            _logger = loggerFactory.CreateLogger<Post>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
        }

        public enum PostStatus
        {
            Queued,
            Posting,
            Posted,
            Faulted,
            SomeFaults
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
            public PostStatus Status { get; set; }
            public string MediaSavedUri { get; set; }
        }

        [OpenApiOperation(tags: ["post"], Summary = "", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PostResponse), Summary = "Returns with details of the post", Description = "Returns with details of the post")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PostParameters), Required = true)]
        [Function("Post")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            string postId = Guid.NewGuid().ToString();
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
                    BlobContainerClient _containerClient;
                    // Key is valid for user
                    _containerClient = _blobStorageAccount.GetBlobContainerClient("post/"+postId); // Root post containing folder

                    // Given the max size of a queue item is 64Kb, we save off anything we can to blob storage for the post

                    // Extract out and save the common post data to storage
                    // Save Description and tags data
                    _containerClient.UploadBlob("description", BinaryData.FromString(para.Description));
                    _containerClient.UploadBlob("description-html", BinaryData.FromString(para.HTMLDescription));
                    _containerClient.UploadBlob("tags", BinaryData.FromString(JsonConvert.SerializeObject(para.Tags)));
                    // Save Images 
                    // TODO: Images, pull them over from the temporary upload location

                    // Extract out and save the PLATFORM SPECIFIC post data to storage
                    foreach (var targetPlatform in para.TargetPlatforms)
                    {
                        // Save the post to queue - Parse out the targets and convert each to a QueueEntity and save to queue
                        QueueEntry queueEntry = new QueueEntry()
                        {
                            PostAt = para.PostAt,
                            RootPostId = postId,
                            PostId = Guid.NewGuid().ToString(),
                            User = para.APIKey.UserID,
                            TargetPlatformServiceId = targetPlatform,
                            Status = (int)PostStatus.Queued
                        };

                        // Write a "lock" file so we don't try and delete the root containing post folder with the data
                        _containerClient.UploadBlob("lock-" + targetPlatform, BinaryData.FromString("LOCKED"));

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
