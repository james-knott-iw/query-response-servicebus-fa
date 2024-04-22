// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using System;
using System.Text.Json;
using Azure.Messaging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace IntegrationWorks.Function
{

    public class Order
    {
        public required int Id { get; set; }
        public required string Name { get; set; }
    }

    public class EventGridTrigger1
    {
        private readonly ILogger<EventGridTrigger1> _logger;
        int id = int.MinValue;

        public EventGridTrigger1(ILogger<EventGridTrigger1> logger)
        {
            _logger = logger;
        }

        readonly Order[] orders = [new Order() { Id = 0, Name = "Chair" }, new Order() { Id = 1, Name = "Couch" }, new Order() { Id = 2, Name = "Table" }, new Order() { Id = 3, Name = "Mat" }, new Order() { Id = 4, Name = "Door" }];


        [Function("provider")]
        public async Task RunAsync([EventGridTrigger] CloudEvent cloudEvent)
        {

            string connectionString = Environment.GetEnvironmentVariable("ORDER_READ_QUEUE_KEY");
            string queueName = "order-read-queue";
            ServiceBusClient client;
            ServiceBusProcessor processor;
            client = new ServiceBusClient(connectionString);
            // create a processor that we can use to process the messages
            processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions());
            _logger.LogInformation("Event type: {type}, Event subject: {subject}", cloudEvent.Type, cloudEvent.Subject);
            try
            {
                _logger.LogInformation("READING MESSAGES");
                // add handler to process messages
                processor.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                processor.ProcessErrorAsync += ErrorHandler;

                // start processing 
                await processor.StartProcessingAsync();
                await Task.Delay(TimeSpan.FromSeconds(3));
                await processor.StopProcessingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing message: {message}", ex.Message);
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                await processor.DisposeAsync();
                await client.DisposeAsync();
                _logger.LogInformation("Disposing of processor and client!");
            }

            //Send reply
            // the sender used to publish messages to the queue
            ServiceBusSender sender;

            string replyConnectionString = Environment.GetEnvironmentVariable("ORDER_READ_REPLY_QUEUE_KEY");
            string replyQueueName = "order-read-reply-queue";
            // Create the clients that we'll use for sending and processing messages.
            client = new ServiceBusClient(replyConnectionString);
            sender = client.CreateSender(replyQueueName);

            //create a batch
            using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();



            if (id != int.MinValue)
            {
                ServiceBusMessage sbMessage;
                if (id >= 0 && id <= 4)
                {
                    Order order = orders[id];
                    sbMessage = new ServiceBusMessage(JsonSerializer.Serialize(order));
                }
                else
                {
                    sbMessage = new ServiceBusMessage($" No order with id {id}.");
                }

                if (!messageBatch.TryAddMessage(sbMessage))
                {
                    // if an exception occurs
                    throw new Exception("Exception has occurred adding message to the Queue.");
                }

                try
                {
                    // Use the producer client to send the batch of messages to the Service Bus queue
                    await sender.SendMessagesAsync(messageBatch);
                    Console.WriteLine($"A batch of three messages has been published to the queue.");
                }
                finally
                {
                    // Calling DisposeAsync on client types is required to ensure that network
                    // resources and other unmanaged objects are properly cleaned up.
                    await sender.DisposeAsync();
                    await client.DisposeAsync();
                }
            }


        }

        // handle received messages
        async Task MessageHandler(ProcessMessageEventArgs args)
        {
            _logger.LogInformation("IN MESSAGE HANDLER");

            string body = args.Message.Body.ToString();
            if (body != null)
            {
                try
                {
                    Message message = JsonSerializer.Deserialize<Message>(body);
                    if (message != null)
                    {
                        id = message.Id;
                        _logger.LogInformation("Received: {body} with Id : {id}", body, id);
                    }
                    else
                    {
                        _logger.LogError("message is null!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error processing message: {message}", ex.Message);
                }
            }
            else
            {
                _logger.LogError("Error processing message: Message body was null.");
            }
            await args.CompleteMessageAsync(args.Message);
        }

        // handle any errors when receiving messages
        Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError("Receiving message errors: {err}", args.Exception.ToString());
            return Task.CompletedTask;
        }
    }
}
