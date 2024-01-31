using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

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

        public async Task QueueMessage()
        {
            // the sender used to publish messages to the queue
            ServiceBusSender sender;

            // number of messages to be sent to the queue
            const int numOfMessages = 3;

            sender = _serviceBusClient.CreateSender(_configuration.GetValue<string>("SERVICE_BUS_QUEUE_NAME"));

            // create a batch 
            using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

            for (int i = 1; i <= numOfMessages; i++)
            {
                // try adding a message to the batch
                if (!messageBatch.TryAddMessage(new ServiceBusMessage($"Message {i}")))
                {
                    // if it is too large for the batch
                    throw new Exception($"The message {i} is too large to fit in the batch.");
                }
            }

            try
            {
                // Use the producer client to send the batch of messages to the Service Bus queue
                await sender.SendMessagesAsync(messageBatch);
                Console.WriteLine($"A batch of {numOfMessages} messages has been published to the queue.");
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
