using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

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
            _logger.LogInformation("Begin reset assets on account: {name}", account.Data.Name);

            var storage = await _resourceProvider.GetStorageAccountAsync(account, cancellationToken);       
            AsyncPageable<MediaAssetResource> assets = account.GetMediaAssets()
                .GetAllAsync(cancellationToken: cancellationToken);
            List<MediaAssetResource>? assetList = await assets.ToListAsync(cancellationToken);
            int resetedAssetCount = 0;
            foreach (var asset in assetList)
            {
                var container = storage.GetContainer(asset);
                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                    return;
                }

                if (_options.all || (_tracker.GetMigrationStatusAsync(container, cancellationToken).Result.Status == MigrationStatus.Failed))
                {
                    try
                    {
                        BlobContainerProperties properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken);
                        if (properties.Metadata != null && properties.Metadata.Count == 0)
                        {
                            _logger.LogInformation($"Container '{container.Name}' does not have metadata.");
                        }
                        else
                        {   // Clear container metadata
                            properties.Metadata.Clear();
                            var deleteOperation = await container.SetMetadataAsync(properties.Metadata);
                            if (deleteOperation.GetRawResponse().Status == 200)
                            {
                                _logger.LogInformation($"Meda data in Container '{container.Name}' is deleted successfully.");
                                resetedAssetCount++;
                            }
                            else
                            {
                                _logger.LogInformation($"Meda data in Container '{container.Name}' does not exist or was not deleted.");
                            }
                        }
                      
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"An unexpected error occurred: {ex.Message}");
                    }

                }
            }
            _logger.LogDebug($"{resetedAssetCount} out of {assetList.Count} assets has been reseted.");
        }
    }
}
