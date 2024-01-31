using AMSMigrate.Contracts;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using MigrateAsset.models;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
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

try
{
    var message = await receiver.ReceiveMessageAsync();
    var content = JsonSerializer.Deserialize<MigrateAssetMessage>(message.Body.ToString());

    var subscriptionId = content?.SubscriptionId;
    var resourceGroup = content?.ResourceGroup;
    var sourceStorageAccountName = content?.SourceStorageAccountName;
    var targetStorageAccountName = content?.TargetStorageAccountName;
    var assetName = content?.AssetName;

    await AMSMigrate.ContainerMigrator.MigrateAsset(subscriptionId!, resourceGroup!, sourceStorageAccountName!, targetStorageAccountName!, assetName!);

    //var output = await AMSMigrate.Program.Main(arguments);

    await receiver.CompleteMessageAsync(message);
}
finally
{
    await receiver.DisposeAsync();
    await client.DisposeAsync();
}
