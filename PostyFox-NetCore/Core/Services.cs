using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using PostyFox_DataLayer;
using PostyFox_DataLayer.TableEntities;
using PostyFox_NetCore.Helpers;
using PostyFox_Secrets;

namespace PostyFox_NetCore
{
    public class Services
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _configTable;
        private readonly ISecureStore? _secureStore;

        public class ServiceRequest
        {
            public string ID { get; set; }
        }

        public Services(ILoggerFactory loggerFactory, IAzureClientFactory<TableServiceClient> clientFactory, ISecureStore? secureStore)
        {
            _logger = loggerFactory.CreateLogger<Services>();
            _configTable = clientFactory.CreateClient("ConfigTable");
            _secureStore = secureStore;
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
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<ServiceDTO>), Summary = "List of services", Description = "This returns the response of available services, a list of objects")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
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
                        ID = service.RowKey,
                        ServiceID = service.ServiceID,
                        ServiceName = service.ServiceName,
                        IsEnabled = service.IsEnabled, // Not sure if this will actually have a use for the "Available" definition? 
                        Configuration = service.Configuration, // In this context, configuration will define what needs to be provided
                        SecureConfiguration = service.SecureConfiguration
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

        [OpenApiOperation(tags: ["services"], Summary = "Fetch Specific Available Service", Description = "", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ServiceDTO), Summary = "", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [Function("Services_GetAvailableService")]
        public HttpResponseData GetAvailableService([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            string serviceName = req.Query["service"];
            ServiceDTO? dto = null;
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                if (!string.IsNullOrEmpty(serviceName)) {
                    _configTable.CreateTableIfNotExists("AvailableServices");
                    string userId = AuthHelper.GetAuthId(req);

                    List<ServiceDTO> ls = new();
                    var client = _configTable.GetTableClient("AvailableServices");
                    var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == "Service" && x.RowKey == serviceName).FirstOrDefault();
                    
                    if (query != null)
                    {
                        dto = new()
                        {
                            ID = query.RowKey,
                            ServiceID = query.ServiceID,
                            ServiceName = query.ServiceName,
                            IsEnabled = query.IsEnabled, // Not sure if this will actually have a use for the "Available" definition? 
                            Configuration = query.Configuration, // In this context, configuration will define what needs to be provided
                            SecureConfiguration = query.SecureConfiguration
                        };
                    }
                }
                var response = req.CreateResponse(HttpStatusCode.OK);
                var valueTask = response.WriteAsJsonAsync(dto);
                valueTask.AsTask().GetAwaiter().GetResult();
                return response;
            }
            else
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                return response;
            }
        }

        [OpenApiOperation(tags: ["services"], Summary = "Fetch User Services", Description = "Fetches the state of configured user services", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<ServiceDTO>), Summary = "List of user configured services", Description = "This returns the response of configured services, a list of objects")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [Function("Services_GetUserServices")]
        public HttpResponseData GetUserServices([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
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
                        ID = service.RowKey,
                        ServiceID = service.ServiceID,
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

        [OpenApiOperation(tags: ["services"], Summary = "Fetch User Service", Description = "Fetches the state of a configured user service", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ServiceDTO), Summary = "A single user service", Description = "This returns the response of configured service")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [Function("Services_GetUserService")]
        public HttpResponseData GetUserService([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Check if authenticated on AAD; if not, return 401 Unauthorized.
            // To do this need to extract the claim and see - this is done on the headers - detailed here
            if (AuthHelper.ValidateAuth(req, _logger))
            {
                _configTable.CreateTableIfNotExists("ConfigTable");
                string userId = AuthHelper.GetAuthId(req);
                string requestBody = new StreamReader(req.Body).ReadToEnd();

                string serviceName = req.Query["service"];
                string serviceId = req.Query["serviceId"];

                _logger.LogInformation("Request for user services received", userId);

                // Check what services the user has enabled, and return a config object
                List<ServiceDTO> ls = new();
                var client = _configTable.GetTableClient("ConfigTable");
                var query = client.Query<ServiceTableEntity>(x => x.PartitionKey == userId && x.ServiceID == serviceId && x.ServiceName == serviceName);
                foreach (var service in query.AsEnumerable())
                {
                    ServiceDTO dto = new()
                    {
                        ID = service.RowKey,
                        ServiceID = service.ServiceID,
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


        [OpenApiOperation(tags: ["services"], Summary = "Set Details for a user service", Description = "Saves the configuration for a service for a given user", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Summary = "The result of the save operation", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ServiceDTO), Required = true)]
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
                ServiceDTO data = JsonConvert.DeserializeObject<ServiceDTO>(requestBody);

                if (data != null)
                {
                    // Unpack and do an upsert
                    ServiceTableEntity tableEntity = new()
                    {
                        PartitionKey = userId,
                        ServiceID = data.ServiceID,
                        ServiceName = data.ServiceName,
                        Configuration = data.Configuration,
                        Timestamp = DateTime.UtcNow,
                        IsEnabled = data.IsEnabled.Value,
                        RowKey = data.ID
                    };
                    client.UpsertEntity(tableEntity);

                    if (data.SecureConfiguration != null)
                    {
                        // We have secure configuration data to unwrap and save to Key Vault
                        if (_secureStore != null)
                        {
                            // Key Vault Secrets have a max of 127 chars in length.  This should be around 73 / 74 chars.
                            _secureStore.SetSecretAsync(data.ID + "-" + userId, data.SecureConfiguration).GetAwaiter().GetResult();
                        }
                    }
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
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Summary = "The result of the delete operation", Description = "")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "Not logged in", Description = "Reauthenticate and ensure auth headers are provided")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ServiceDTO), Required = true)]
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
