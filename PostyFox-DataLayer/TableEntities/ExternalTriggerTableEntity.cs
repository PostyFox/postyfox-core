using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostyFox_DataLayer.TableEntities
{
    public class ExternalTriggerTableEntity : ITableEntity
    {
        [Description("UserID")]
        public string PartitionKey { get; set; }
        [Description("Unique External Trigger ID")]
        public string RowKey { get; set; }
        public string TwitchAccount { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
