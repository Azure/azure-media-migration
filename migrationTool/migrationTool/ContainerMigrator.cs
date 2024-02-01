using AMSMigrate.Ams;
using AMSMigrate.Azure;
using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMSMigrate
{
    public static class ContainerMigrator
    {
        public static async Task<AssetMigrationResult> MigrateAsset(
            string subscriptionId, 
            string resourceGroup,
            string sourceStorageAccountName, 
            string targetStorageAccountName, 
            string containerName)
        {
            var services = ConfigureServices();

            var serviceProvider = services.BuildServiceProvider();

            var tracker = serviceProvider.GetRequiredService<IMigrationTracker<BlobContainerClient, AssetMigrationResult>>();
            var credentials = serviceProvider.GetRequiredService<TokenCredential>();
            var transformFactory = serviceProvider.GetRequiredService<TransformFactory>();
            var logger = serviceProvider.GetRequiredService<ILogger<StorageMigrator>>();

            var migrationHandler = new MigrationHandler(tracker, credentials, transformFactory, logger);

            //await migrationHandler.Migrate("slothmedia", "amsmigrationtarget", "asset-f935fe12-02b5-45f7-a861-b1419a397f16");
            var result = await migrationHandler.Migrate(subscriptionId, resourceGroup, sourceStorageAccountName, targetStorageAccountName, containerName);

            return result;
        }

        private static IServiceCollection ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddSingleton<IMigrationTracker<BlobContainerClient, AssetMigrationResult>, AssetMigrationTracker>()
                .AddSingleton<TokenCredential>(new DefaultAzureCredential(includeInteractiveCredentials: true))
                .AddSingleton<TransformFactory>()
                .AddLogging(builder =>
                {
                    builder
                        .SetMinimumLevel(LogLevel.Trace)
                        .AddSerilog(dispose: true);

                    builder.AddConsole(builder =>
                    {
                        builder.FormatterName = ConsoleFormatterNames.Simple;
                        builder.LogToStandardErrorThreshold = LogLevel.Debug;
                    });
                })
                .Configure<SimpleConsoleFormatterOptions>(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                    options.IncludeScopes = false;
                })
                .AddSingleton<ICloudProvider, AzureProvider>()
                .AddSingleton<AzureResourceProvider>()
                .AddSingleton<TemplateMapper>();

            return services;
        }
    }
}
