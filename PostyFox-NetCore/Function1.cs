using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace PostyFox_NetCore
{
    public class Services
    {
        private readonly ILogger _logger;

        public Services(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Services>();
        }

        [Function("Services")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            // Check if authenticated on AAD; if not, return 403 Unauthorized.

            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
