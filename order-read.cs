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

                return new OkObjectResult("Welcome to Azure Functions!");
            }
            return new OkObjectResult("Connection String null");
        }

    }
}
