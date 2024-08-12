using Azure;
using Azure.Data.Tables;
using System.ComponentModel;

namespace PostyFox_DataLayer.TableEntities
{
#pragma warning disable CS8618
    public class PostingTemplateTableEntity : ITableEntity
    {
        [Description("User ID")]
        public string PartitionKey { get; set; }
        [Description("ID")]
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
#pragma warning restore CS8618
}
