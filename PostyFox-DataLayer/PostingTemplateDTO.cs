using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace PostyFox_DataLayer
{
    public class PostingTemplateDTO
    {
        [OpenApiProperty(Description = "The internal unique identifier for this Posting Template")]
        public string? ID { get; set; }

        [OpenApiProperty(Description = "Title to be used for services which support a title")]
        public string? Title { get; set; }

        [OpenApiProperty(Description = "Markdown Compatible Body")]
        public string? MarkdownBody { get; set; }
    }
}
