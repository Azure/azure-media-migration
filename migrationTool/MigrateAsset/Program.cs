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

Console.WriteLine("START");

try
{
    var message = await receiver.ReceiveMessageAsync();
    var body = message.Body.ToString();
    var content = JsonSerializer.Deserialize<MigrateAssetMessage>(body);

    var subscriptionId = content?.SubscriptionId;
    var resourceGroup = content?.ResourceGroup;
    var sourceStorageAccountName = content?.SourceStorageAccountName;
    var targetStorageAccountName = content?.TargetStorageAccountName;
    var assetName = content?.AssetName;

    Console.WriteLine($"CALL MIGRATOR - message: {body}" );

    var result = await AMSMigrate.ContainerMigrator.MigrateAsset(subscriptionId!, resourceGroup!, sourceStorageAccountName!, targetStorageAccountName!, assetName!);

    Console.WriteLine($"COMPLETE MESSAGE - result: {result.OutputPath}");

    await receiver.CompleteMessageAsync(message);
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.ToString()}", ex);
}
finally
{
    await receiver.DisposeAsync();
    await client.DisposeAsync();
}
