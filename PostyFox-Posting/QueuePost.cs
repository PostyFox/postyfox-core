using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PostyFox_Posting
{
    public class QueuePost
    {
        private readonly ILogger<QueuePost> _logger;

        public QueuePost(ILogger<QueuePost> logger)
        {
            _logger = logger;
        }

        //[Function(nameof(QueuePost))]
        //public void Run([QueueTrigger("postingqueue", Connection = "PostingQueue")] QueueMessage message)
        //{
        //    _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");
        //}
    }
}
