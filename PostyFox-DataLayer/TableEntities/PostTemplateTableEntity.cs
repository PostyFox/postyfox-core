using Azure;
using Azure.Data.Tables;
using System.ComponentModel;

namespace PostyFox_DataLayer.TableEntities
{
    public class PostTemplateTableEntity : ITableEntity
    {
        [Description("UserID")]
        public string PartitionKey { get; set; }
        [Description("Unique External Trigger ID")]
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        // TODO: We should support an image being referenced here
        // TODO: We should support "chaining" multiple posts together for allowing posting of threads
    }
}
