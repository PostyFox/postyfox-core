using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;

namespace PostyFox_NetCore.Helpers
{
    internal class AuthHelper
    {
        // Header to inspect on the request is X-MS-CLIENT-PRINCIPAL-NAME
        // If this is empty or not set then we are not authenticated.

        // HttpResponseData GetUserStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)

        const string AUTH_HEADER = "X-MS-CLIENT-PRINCIPAL-NAME";
        const string AUTH_HEADER_ID = "X-MS-CLIENT-PRINCIPAL-ID";

        public static string GetAuthId(HttpRequestData request)
        {
            if (request.Headers.Contains(AUTH_HEADER_ID))
            {
                return request.Headers.GetValues(AUTH_HEADER_ID).First();
            }
            return string.Empty;
        }

        public static bool ValidateAuth(HttpRequestData request)
        {
            if (request.Headers.Contains(AUTH_HEADER))
            {
                // Auth_header will contain a human readable version of the logged in name
                ClaimsPrincipal principal = ClaimsPrincipalParser.Parse(request);
                if (principal.Identity != null && principal.Identity.IsAuthenticated) 
                {
                    return true;
                } 
                else
                {
                    return false;
                }
            } 
            else
            {
                return false;
            }
        }
    }
}
