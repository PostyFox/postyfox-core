using Azure;
using Azure.Data.Tables;
using System.ComponentModel;

namespace PostyFox_DataLayer.TableEntities
{
    // An entry for each External Trigger that a user wants to add
    // This entry essentially is the "glue" to tie other elements up

    public class ExternalTriggerTableEntity : ITableEntity
    {
        [Description("UserID")]
        public string PartitionKey { get; set; }
        [Description("Unique External Trigger ID")]
        public string RowKey { get; set; }
        public string ExternalAccount { get; set; }
        [Description("External Trigger type (only twitch supported here for now)")]
        public string ExternalAccountType { get; set; }
        public string Template { get; set; }
        public string TargetPlatform { get; set; }
        public int NotifyFrequencyHrs { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
