using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using System.Configuration;
using Microsoft.Extensions.Configuration;

namespace IntegrationWorks.Function
{

    public class Message
    {
        public required int Id { get; set; }
    }
    public class order_read
    {
        private readonly ILogger<order_read> _logger;


        public order_read(ILogger<order_read> logger)
        {

            _logger = logger;



        }

        [Function("order_read")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "order_read/{id:int}")] HttpRequestData req, int id)
        {

            string? connectionString = System.Environment.GetEnvironmentVariable("ORDER_READ_QUEUE_KEY");
            if (connectionString != null)
            {
                string queueName = "order-read-queue";

                // the client that owns the connection and can be used to create senders and receivers
                ServiceBusClient client;

                // the sender used to publish messages to the queue
                ServiceBusSender sender;

                client = new ServiceBusClient(connectionString);
                sender = client.CreateSender(queueName);

                // create a batch
                using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

                Message message = new Message { Id = id };

                if (!messageBatch.TryAddMessage(new ServiceBusMessage(JsonSerializer.Serialize(message))))
                {

                    throw new Exception($"Exception {message.Id} has occured.");
                }

                try
                {
                    await sender.SendMessagesAsync(messageBatch);
                    _logger.LogInformation($"Sent message: {message} to the queue");
                }
                finally
                {
                    // Calling DisposeAsync on client types is required to ensure that network
                    // resources and other unmanaged objects are properly cleaned up.
                    await sender.DisposeAsync();
                    await client.DisposeAsync();
                }

                _logger.LogInformation("C# HTTP trigger function processed a request.");
                return new OkObjectResult("Welcome to Azure Functions!");
            }
            return new OkObjectResult("Connection String null");
        }

    }
}
