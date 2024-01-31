using Azure.Messaging.ServiceBus;
using migrationApi.Models;
using System.Text.Json;

namespace migrationApi.Services
{
    public class ServiceBusService
    {
        private ServiceBusClient _serviceBusClient;
        private IConfiguration _configuration;

        public ServiceBusService(ServiceBusClient serviceBusClient, IConfiguration configuration)
        {
            _serviceBusClient = serviceBusClient;
            _configuration = configuration;
        }

        public async Task QueueMessage(MigrationRequest migrationRequest)
        {
            var sender = _serviceBusClient.CreateSender(_configuration.GetValue<string>("SERVICEBUS_QUEUE"));

            // create a batch 
            using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

            if (!messageBatch.TryAddMessage(new ServiceBusMessage(JsonSerializer.Serialize(migrationRequest))))
            {
                // if it is too large for the batch
                throw new Exception($"The message is too large to fit in the batch.");
            }

            try
            {
                // Use the producer client to send the batch of messages to the Service Bus queue
                await sender.SendMessagesAsync(messageBatch);
                Console.WriteLine($"A batch of messages has been published to the queue.");
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                await sender.DisposeAsync();
            }
        }
    }
}
