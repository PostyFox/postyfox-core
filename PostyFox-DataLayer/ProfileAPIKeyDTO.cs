using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace PostyFox_DataLayer
{
    public class ProfileAPIKeyDTO
    {
        [OpenApiProperty(Description = "The internal unique identifier for this API Key")]
        public string? ID { get; set; }

        [OpenApiProperty(Description = "API Key")]
        public string? APIKey { get; set; }

        [OpenApiProperty(Description = "User ID")]
        public string? UserID { get; set; }
    }
}
