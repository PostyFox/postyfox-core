using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PostyFox_NetCore.Helpers;

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
        public HttpResponseData GetUserStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            // Check if authenticated on AAD; if not, return 403 Unauthorized.
            // To do this need to extract the claim and see - this is done on the headers - detailed here
            if (AuthHelper.ValidateAuth(req))
            {

                _logger.LogInformation("C# HTTP trigger function processed a request.");

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");


                response.WriteString("Welcome to Azure Functions!");
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
