using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PostyFox_Posting
{
    public class GeneratePost
    {
        private readonly ILogger<GeneratePost> _logger;

        public GeneratePost(ILogger<GeneratePost> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GeneratePost))]
        public void Run([QueueTrigger("generatequeue")] QueueMessage message)
        {
            // Verify that the message is valid and has an auth token

            // Generate the post from the information in the message

            // Queue up the post for posting

            _logger.LogInformation($"Processing message: {message.MessageText}");
        }
    }
}
