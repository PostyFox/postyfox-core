﻿using Azure;
using Azure.Data.Tables;
using System.ComponentModel;

namespace PostyFox_DataLayer.TableEntities
{
#pragma warning disable CS8618
    public class ServiceTableEntity : ITableEntity
    {
        public string ServiceName { get; set; }
        public bool IsEnabled { get; set; }
        public string Configuration { get; set; }
        /// <summary>
        /// Secure Configuration definitions; only set on GetAvailable calls; for user set definitions will contain a pointer for KV entries.
        /// </summary>
        public string? SecureConfiguration { get; set; }
        [Description("User ID")]
        public string PartitionKey { get; set; }
        public string? ServiceID { get; set; }
        [Description("ID")]
        public string RowKey { get; set; }
        public string Endpoint { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
#pragma warning restore CS8618
}
