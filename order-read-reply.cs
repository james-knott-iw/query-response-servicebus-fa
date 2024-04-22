// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using System;
using System.Text.Json;
using Azure.Messaging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace IntegrationWorks.Function
{
    public class order_read_reply
    {
        private readonly ILogger<order_read_reply> _logger;

        public order_read_reply(ILogger<order_read_reply> logger)
        {
            _logger = logger;
        }

        [Function(nameof(order_read_reply))]
        public async Task RunAsync([EventGridTrigger] CloudEvent cloudEvent)
        {
            string replyConnectionString = Environment.GetEnvironmentVariable("ORDER_READ_REPLY_QUEUE_KEY");
            string replyQueueName = "order-read-reply-queue";
            ServiceBusClient client;
            ServiceBusProcessor processor;
            client = new ServiceBusClient(replyConnectionString);
            // create a processor that we can use to process the messages
            processor = client.CreateProcessor(replyQueueName, new ServiceBusProcessorOptions());
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
                _logger.LogError("Error processing Order message: {message}", ex.Message);
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                await processor.DisposeAsync();
                await client.DisposeAsync();
                _logger.LogInformation("Disposing of processor and client!");
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError("Receiving message errors: {err}", args.Exception.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            _logger.LogInformation("IN MESSAGE HANDLER");

            string body = args.Message.Body.ToString();
            if (body != null)
            {
                try
                {
                    Order order = JsonSerializer.Deserialize<Order>(body);
                    if (order != null)
                    {

                        _logger.LogInformation("Received: order with Id: {id} and Name: {name}", order.Id, order.Name);
                    }
                    else
                    {
                        _logger.LogError("Order is null!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error processing Order: {message}", ex.Message);
                }
            }
            else
            {
                _logger.LogError("Error processing message: Message body was null.");
            }
            await args.CompleteMessageAsync(args.Message);
        }
    }
}
