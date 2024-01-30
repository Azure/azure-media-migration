using Azure.Identity;
using Azure.Messaging.ServiceBus;
using MigrateAsset.models;
using System.Text.Json;

ServiceBusClient client;
ServiceBusReceiver receiver;

// "<NAMESPACE-NAME>.servicebus.windows.net"
string serviceBusNamespace = Environment.GetEnvironmentVariable("SERVICEBUS_NAMESPACE") ?? string.Empty;
string serviceBusFqdn = $"{serviceBusNamespace}.servicebus.windows.net";

// <QUEUE-NAME>
string serviceBusQueue = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE") ?? string.Empty;

var clientOptions = new ServiceBusClientOptions()
{
    TransportType = ServiceBusTransportType.AmqpWebSockets
};
client = new ServiceBusClient(serviceBusFqdn,
    new DefaultAzureCredential(), clientOptions);

receiver = client.CreateReceiver(serviceBusQueue, new ServiceBusReceiverOptions());

Console.WriteLine("START");

try
{
    var message = await receiver.ReceiveMessageAsync();
    var content = JsonSerializer.Deserialize<MigrateAssetMessage>(message.Body.ToString());

    var targetStorageAccount = $"https://{content?.TargetStorageAccountName}.blob.core.windows.net";
    var filter = $"name eq '{content?.AssetName}'";

    var arguments = new string[]
    {
        "assets",
        "-s", content?.SubscriptionId,
        "-g", content?.ResourceGroup,
        "-n", content?.MediaServiceName,
        "-o", targetStorageAccount,
        "-f", filter
    };

    Console.WriteLine("CALL MIGRATOR");

    var output = await AMSMigrate.Program.Main(arguments);

    Console.WriteLine("COMPLETE MESSAGE");

    await receiver.CompleteMessageAsync(message);
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}", ex);
}
finally
{
    await receiver.DisposeAsync();
    await client.DisposeAsync();
}
