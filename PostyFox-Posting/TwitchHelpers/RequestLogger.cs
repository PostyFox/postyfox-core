using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PostyFox_Posting.TwitchHelpers
{
    internal static class RequestLogger
    {
        internal static bool LogRequest(string request, string messageId, TableClient tableClient)
        {

            //    var tableEntity = new TableEntity
            //    {
            //        PartitionKey = "Twitch",
            //        RowKey = messageId,

            //    };
            //    var transactions = new List<TableTransactionAction>
            //{
            //    new TableTransactionAction(TableTransactionActionType.UpsertReplace, tableEntity)
            //};
            //    tableClient.SubmitTransaction(transactions);


            return true;
        }
    }
}
