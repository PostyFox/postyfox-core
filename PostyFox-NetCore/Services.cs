using System.Net;
using Azure.Data.Tables;
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
    public class Services
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;

        public Services(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory)
        {
            _logger = loggerFactory.CreateLogger<Services>();
            _configTable = clientFactory.CreateClient("ConfigTable");
        }

        [Function("Services_Ping")]
        public HttpResponseData Ping([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            var valueTask = response.WriteAsJsonAsync(req.Headers.ToString());
            valueTask.AsTask().GetAwaiter().GetResult();
            return response;
        }

        [OpenApiOperation(tags: ["services"], Summary = "Fetch All Available Services", Description = "Fetch all available services a user can configure on the platform", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(string), Summary = "List of services", Description = "This returns the response")]
        [Function("Services_GetAvailable")]
        public HttpResponseData GetAvailable([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("AvailableServices");
                string userId = AuthHelper.GetAuthId(req);

                List<ServiceDTO> ls = new();
                var client = _configTable.GetTableClient("AvailableServices");
                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == "Service");
                foreach (var service in query.AsEnumerable())
                {
                    ServiceDTO dto = new()
                    {
                        ServiceID = service.RowKey,
                        ServiceName = service.ServiceName,
                        IsEnabled = service.IsEnabled, // Not sure if this will actually have a use for the "Available" definition? 
                        Configuration = service.Configuration // In this context, configuration will define what needs to be provided
                    };
                    ls.Add(dto);
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

        [OpenApiOperation(tags: ["services"], Summary = "Fetch User Services", Description = "Fetches the state of configured and available user services", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(string), Summary = "List of user configured services", Description = "This returns the response")]
        [Function("Services_GetUserService")]
        public HttpResponseData GetUserService([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Check if authenticated on AAD; if not, return 401 Unauthorized.
            // To do this need to extract the claim and see - this is done on the headers - detailed here
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("ConfigTable");
                string userId = AuthHelper.GetAuthId(req);

                _logger.LogInformation("Request for user services received", userId);

                // Check what services the user has enabled, and return a config object
                List<ServiceDTO> ls = new();
                var client = _configTable.GetTableClient("ConfigTable");
                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == userId);
                foreach(var service in query.AsEnumerable())
                {
                    ServiceDTO dto = new()
                    {
                        ServiceID = service.RowKey,
                        ServiceName = service.ServiceName,
                        IsEnabled = service.IsEnabled,
                        Configuration = service.Configuration
                    };
                    ls.Add(dto);
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

        [OpenApiOperation(tags: ["services"], Summary = "Set Details for a user service", Description = "Saves the configuration for a services for a given user", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(string), Summary = "The response", Description = "This returns the response")]
        [Function("Services_SetUserService")]
        public HttpResponseData SetUserService([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("ConfigTable");
                var client = _configTable.GetTableClient("ConfigTable");
                string userId = AuthHelper.GetAuthId(req);

                _logger.LogInformation("SetUser Called", userId);

                string requestBody = new StreamReader(req.Body).ReadToEnd();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                if (data != null)
                {
                    // Unpack and do an upsert
                    ServiceTableEntity tableEntity = new()
                    {
                        PartitionKey = userId,
                        ServiceName = data.ServiceName,
                        Configuration = data.Configuration,
                        Timestamp = DateTime.UtcNow,
                        IsEnabled = data.Enabled,
                        RowKey = data.ServiceID
                    };
                    client.UpsertEntity(tableEntity);
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

        [OpenApiOperation(tags: ["services"], Summary = "Delete a service a user has configured", Description = "Delete a user that a user has already configured and no longer wants to use", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(string), Summary = "The response", Description = "This returns the response")]
        [Function("Services_DeleteUserService")]
        public HttpResponseData DeleteUserService([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req)
        {
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("ConfigTable");
                var client = _configTable.GetTableClient("ConfigTable");
                string userId = AuthHelper.GetAuthId(req);

                string requestBody = new StreamReader(req.Body).ReadToEnd();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                if (data != null)
                {
                    client.DeleteEntity(userId, data.ServiceID);
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
