using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Extensions.Msal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AMSMigrate
{
    internal class MigrationHandler
    {
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;
        private readonly TokenCredential _credential;
        private readonly TransformFactory _transformFactory;
        private readonly ILogger<StorageMigrator> _logger;

        public MigrationHandler(
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            TokenCredential credential,
            TransformFactory transformFactory,
            ILogger<StorageMigrator> logger) 
        {
            _tracker = tracker;
            _credential = credential;
            _transformFactory = transformFactory;
            _logger = logger;
        }

        public async Task<AssetMigrationResult> Migrate(
            string subscriptionId,
            string resourceGroup, 
            string accountName, 
            string targetStorageName, 
            string containerName)
        {
            var globaloptions = new GlobalOptions(subscriptionId, resourceGroup, CloudType.Azure, LogLevel.Debug, "");

            var storageOptions = new StorageOptions
            (
                accountName,
                targetStorageName,
                Contracts.Packager.Shaka,
                "${ContainerName}/",
                null,
                containerName,
                Path.Combine(Path.GetTempPath(), "AMSMigrate"),
                true,
                true,
                true,
                false,
                false,
                2,
                1,
                false,
                null
            );

            var cancelationToken = new CancellationToken();

            var storageMigrator = new StorageMigrator(globaloptions, storageOptions, _tracker, _credential, _transformFactory, _logger);
            var result = await storageMigrator.MigrateAsync(cancelationToken);

            return result;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Register services required by StorageMigrator and other dependencies  
            services.AddTransient<StorageMigrator>();
            services.AddTransient<GlobalOptions>();
            // ... other services and configurations  
        }

    }
}
