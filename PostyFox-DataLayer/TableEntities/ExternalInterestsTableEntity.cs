using Azure;
using Azure.Data.Tables;
using System.ComponentModel;

namespace PostyFox_DataLayer.TableEntities
{
    // This will be an entry per twitch stream added to the system for external triggers.
    // The configuration value will be a json array of user accounts & rowkey's that are interested in receiving notifications

    public class ExternalInterestsTableEntity : ITableEntity
    {
        [Description("External Service Type (i.e. Twitch)")]
        public string PartitionKey { get; set; }
        [Description("Twitch (etc) User ID")]
        public string RowKey { get; set; }
        public string Configuration { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
