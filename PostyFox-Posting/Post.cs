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
using Azure.Storage.Queues;

namespace PostyFox_Posting
{
    public class Post
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly BlobServiceClient _blobStorageAccount;
        private readonly QueueServiceClient _postingQueueClient;

        public Post(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, IAzureClientFactory<BlobServiceClient> blobClientFactory, IAzureClientFactory<QueueServiceClient> postingQueueFactory)
        {
            _logger = loggerFactory.CreateLogger<Post>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
            _postingQueueClient = postingQueueFactory.CreateClient("PostingQueue");
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
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Summary = "", Description = "")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PostParameters), Required = true)]
        [Function("Post")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            string postId = Guid.NewGuid().ToString().Replace("-", "");
            // Confirm that have a API Key
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            PostParameters para = JsonConvert.DeserializeObject<PostParameters>(requestBody);
            if (para != null)
            {
                // Lookup against the keys, check that we have a valid key before we continue
                _logger.LogDebug("Validating API Key data", para);

                _configTable.CreateTableIfNotExists("UserProfilesAPIKeys");

                string userId = para.APIKey.UserID.ToLower();

                var client = _configTable.GetTableClient("UserProfilesAPIKeys");
                var query = client.Query<ProfileAPIKeyTableEntity>(x => x.PartitionKey == userId && x.RowKey == para.APIKey.ID);
                var valid = query.FirstOrDefault();
                if (valid != null)
                {
                    BlobContainerClient _postContainerClient = _blobStorageAccount.GetBlobContainerClient("post");
                    _postContainerClient.CreateIfNotExists();
                    //_postContainerClient.

                    //BlobContainerClient _containerClient = _blobStorageAccount.GetBlobContainerClient("post/"+postId); // Root post containing folder
                    //_containerClient.CreateIfNotExists();

                    // Given the max size of a queue item is 64Kb, we save off anything we can to blob storage for the post

                    // Extract out and save the common post data to storage
                    // Save Description and tags data
                    _postContainerClient.UploadBlob(postId + "/description", BinaryData.FromString(para.Description));
                    _postContainerClient.UploadBlob(postId + "/description-html", BinaryData.FromString(para.HTMLDescription));
                    _postContainerClient.UploadBlob(postId + "/tags", BinaryData.FromString(JsonConvert.SerializeObject(para.Tags)));
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
                        _postContainerClient.UploadBlob(postId + "/lock-" + targetPlatform, BinaryData.FromString("LOCKED"));

                        // Schedule as required

                        QueueClient queueClient = _postingQueueClient.GetQueueClient("postingqueue");
                        queueClient.SendMessage(JsonConvert.SerializeObject(queueEntry));
                        
                    }
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    return response;
                } 
                else
                {
                    _logger.LogDebug("API Key is invalid or could not be verified");

                    var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                    return response;
                }
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                return response;
            }
        }
    }
}
