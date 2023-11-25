using Microsoft.Azure.Functions.Worker.Http;

namespace PostyFox_NetCore.Helpers
{
    internal class AuthHelper
    {
        // Header to inspect on the request is X-MS-CLIENT-PRINCIPAL-NAME
        // If this is empty or not set then we are not authenticated.

        // HttpResponseData GetUserStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)

        const string AUTH_HEADER = "X-MS-CLIENT-PRINCIPAL-NAME";

        public static bool ValidateAuth(HttpRequestData request)
        {
            if (request.Headers.Contains(AUTH_HEADER))
            {
                // Auth_header will contain a human readable version of the logged in name
                return true;
            } 
            else
            {
                return false;
            }
        }
    }
}
