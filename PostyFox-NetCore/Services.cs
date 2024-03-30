using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
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

        [OpenApiOperation(operationId: "getUserStatus", tags: new[] { "services" }, Summary = "Fetch User Services", Description = "Fetches the state of configured and available user services", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Summary = "The response", Description = "This returns the response")]

        [Function("Services")]
        public HttpResponseData GetUserStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Check if authenticated on AAD; if not, return 401 Unauthorized.
            // To do this need to extract the claim and see - this is done on the headers - detailed here
            if (AuthHelper.ValidateAuth(req))
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
                        ServiceID = service.ServiceID,
                        ServiceName = service.ServiceName,
                        IsEnabled = service.IsEnabled
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
    }
}
