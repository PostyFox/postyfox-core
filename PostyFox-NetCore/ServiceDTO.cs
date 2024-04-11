using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace PostyFox_NetCore
{
    public class ServiceDTO
    {
        /// <summary>
        /// The Service name; friendly presentable to the end user (defined by the user)
        /// </summary>
        [OpenApiPropertyAttribute(Description = "The Service name; friendly presentable to the end user (defined by the user)")]
        public string? ServiceName { get; set; }
        /// <summary>
        /// The internal identifier of the service. In combination with the User ID is a unique value.
        /// </summary>
        [OpenApiPropertyAttribute(Description = "The internal identifier of the service. In combination with the User ID is a unique value.")]
        public string? ID { get; set; }
        /// <summary>
        /// Reference to the Service definition (ID)
        /// </summary>
        [OpenApiPropertyAttribute(Description = "Reference to the Service definition (ID)")]
        public string? ServiceID { get; set; }
        /// <summary>
        /// JSON structure representing the Configuration of the Service; if on GetAvailable, this is the *required* configuration for the Service, if on the UserService this is the *saved* configuration
        /// </summary>
        [OpenApiPropertyAttribute(Description = "JSON structure representing the Configuration of the Service; if on GetAvailable, this is the *required* configuration for the Service, if on the UserService this is the *saved* configuration")]
        public string? Configuration { get; set; }
        /// <summary>
        /// JSON structure representing Secure Configuration properties for the service; will be only populated on GetAvailable and empty on UserService.  Values are stored seperately and encrypted.
        /// </summary>
        [OpenApiPropertyAttribute(Description = "JSON structure representing Secure Configuration properties for the service; will be only populated on GetAvailable and empty on UserService.")]
        public string? SecureConfiguration { get; set; }
        /// <summary>
        /// Identifies if the service is available for use, or enabled by the user for posting / interaction
        /// </summary>
        [OpenApiPropertyAttribute(Description = "Identifies if the service is available for use, or enabled by the user for posting / interaction")]
        public bool? IsEnabled { get; set; }
        /// <summary>
        /// Contains details of the endpoint that should be address for this particular Service
        /// </summary>
        [OpenApiPropertyAttribute(Description = "Contains details of the endpoint that should be address for this particular Service")]
        public string Endpoint { get; set; }
    }
}
