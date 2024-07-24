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

        [Function(nameof(QueuePost))]
        public void Run([QueueTrigger("postingqueue")] QueueMessage message)
        {
            _logger.LogInformation($"Processing message: {message.MessageText}");
        }
    }
}
