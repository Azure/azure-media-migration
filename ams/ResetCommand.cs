using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMSMigrate.ams
{
    internal class ResetCommand : BaseMigrator
    {
        private readonly ILogger _logger;
        private readonly ResetOptions _options;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;
        public ResetCommand(GlobalOptions globalOptions,
            ResetOptions resetOptions,
            IAnsiConsole console,
            TokenCredential credential,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            ILogger<ResetCommand> logger)
         : base(globalOptions, console, credential)
        {
            _options = resetOptions;
            _logger = logger;
            _tracker = tracker;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var account = await GetMediaAccountAsync(_options.AccountName, cancellationToken);
            _logger.LogInformation("Begin cleaning up on account: {name}", account.Data.Name);

            var storage = await _resourceProvider.GetStorageAccountAsync(account, cancellationToken);
            var totalAssets = await QueryMetricAsync(
            account.Id.ToString(),
            "AssetCount",
            cancellationToken: cancellationToken);

            _logger.LogInformation("The total asset count of the media account is {count}.", totalAssets);
            AsyncPageable<MediaAssetResource> assets = account.GetMediaAssets()
                .GetAllAsync(cancellationToken: cancellationToken);
            List<MediaAssetResource>? assetList = await assets.ToListAsync(cancellationToken);

            foreach (var asset in assetList)
            {
                var container = storage.GetContainer(asset);
                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                    return;
                }

                // The asset container exists, try to check the metadata list first.

                if (_options.all || (_tracker.GetMigrationStatusAsync(container, cancellationToken).Result.Status == MigrationStatus.Failed))
                {
                    container.DeleteBlobAsync(cancellationToken);
                }
            }
        }
    }
}
