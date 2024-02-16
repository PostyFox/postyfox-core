using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostyFox_NetCore
{
#pragma warning disable CS8618
    public class ServiceTableEntity : ITableEntity
    {
        public string ServiceName { get; set; }
        public string ServiceID { get; set; }
        public bool IsEnabled { get; set; }
        [Description("User ID")]
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
#pragma warning restore CS8618
}
