using Azure;
using Azure.Data.Tables;
using System.ComponentModel;

namespace PostyFox_DataLayer.TableEntities
{
#pragma warning disable CS8618
    public class ProfileAPIKeyTableEntity : ITableEntity
    {
        [Description("UserID")]
        public string PartitionKey { get; set; }
        [Description("Unique API Key ID")]
        public string RowKey { get; set; }
        public string APIKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
#pragma warning restore CS8618
}
