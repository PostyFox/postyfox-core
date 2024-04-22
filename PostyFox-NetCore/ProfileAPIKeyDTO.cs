using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace PostyFox_NetCore
{
    public class ProfileAPIKeyDTO
    {
        [OpenApiPropertyAttribute(Description = "The internal unique identifier for this API Key")]
        public string? ID { get; set; }

        [OpenApiPropertyAttribute(Description = "API Key")]
        public string? APIKey { get; set; }

        [OpenApiPropertyAttribute(Description = "User ID")]
        public string? UserID { get; set; }
    }
}
