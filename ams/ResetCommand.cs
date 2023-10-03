using AMSMigrate.Contracts;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AMSMigrate.Ams
{
    internal class ResetCommand : BaseMigrator
    {
        private readonly ResetOptions _options;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;
        internal const string AssetTypeKey = "AssetType";
        internal const string MigrateResultKey = "MigrateResult";
        internal const string ManifestNameKey = "ManifestName";
        internal const string OutputPathKey = "OutputPath";

        public ResetCommand(GlobalOptions globalOptions,
            ResetOptions resetOptions,
            IAnsiConsole console,
            TokenCredential credential,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            ILogger<ResetCommand> logger)
         : base(globalOptions, console, credential, logger)
        {
            _options = resetOptions;
            _tracker = tracker;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var (isAMSAcc, account) = await IsAMSAccountAsync(_options.AccountName, cancellationToken);
            if (!isAMSAcc)
            {
                var (storageClient, accountId) = await _resourceProvider.GetStorageAccount(_options.AccountName, cancellationToken);
                if (storageClient == null)
                {
                    _logger.LogError("No valid storage account was found.");
                    throw new Exception("No valid storage account was found.");
                }
                _logger.LogInformation("Begin reset storage container: {name}", storageClient.AccountName);
                double totalItems = await GetStorageBlobMetricAsync(accountId, cancellationToken);
                var containers = storageClient.GetBlobContainersAsync(
                               cancellationToken: cancellationToken);
                _logger.LogInformation("The total containers count of the storage account is {count}.", totalItems);
                List<BlobContainerItem>? filteredList = await containers.ToListAsync();
                int resetAssetCount = 0;
                foreach (var container in filteredList)
                {
                    BlobContainerClient containerClient = storageClient.GetBlobContainerClient(container.Name);
                    if (await MigrateTaskAsync(containerClient, cancellationToken))
                        resetAssetCount++;
                }
                _logger.LogDebug("{resetAssetCount} out of {totalAssetCount} assets has been reset.", resetAssetCount, filteredList.Count);
            }
            else
            {
                if (account == null)
                {
                    _logger.LogError("No valid media account was found.");
                    throw new Exception("No valid media account was found.");
                }
                _logger.LogInformation("Begin reset assets on account: {name}", account.Data.Name);
                await _resourceProvider.SetStorageResourceGroupsAsync(account, cancellationToken);
                AsyncPageable<MediaAssetResource> assets = account.GetMediaAssets()
                    .GetAllAsync(cancellationToken: cancellationToken);
                List<MediaAssetResource>? assetList = await assets.ToListAsync(cancellationToken);
                int resetAssetCount = 0;
                foreach (var asset in assetList)
                {
                    var (storage, _) = await _resourceProvider.GetStorageAccount(asset.Data.StorageAccountName, cancellationToken);
                    var container = storage.GetContainer(asset);
                    if (!await container.ExistsAsync(cancellationToken))
                    {
                        _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                        return;
                    }

                    if (await MigrateTaskAsync(container, cancellationToken))
                        resetAssetCount++;
                }
                _logger.LogDebug("{resetAssetCount} out of {totalAssetCount} assets has been reset.", resetAssetCount, assetList.Count);
            }

        }
        private async Task<bool> MigrateTaskAsync(BlobContainerClient container, CancellationToken cancellationToken)
        {
            bool isReseted = false;
            if (_options.Category.Equals("all", StringComparison.OrdinalIgnoreCase) || (_tracker.GetMigrationStatusAsync(container, cancellationToken).Result.Status == MigrationStatus.Failed))
            {
                try
                {
                    BlobContainerProperties properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken);

                    if (properties?.Metadata != null && properties.Metadata.Count == 0)
                    {
                        _logger.LogInformation("Container '{container}' does not have metadata.", container.Name);

                    }
                    else
                    {   // Clear container metadata

                        var isDmtGeneratedContainer = false;
                        var assetType = "";

                        properties?.Metadata?.TryGetValue(AssetTypeKey, out assetType);

                        if (assetType == AssetMigrationResult.AssetType_DmtGenerated)
                        {
                            // It is a container for the DMT generated content, don't reset it.
                            isDmtGeneratedContainer = true;
                            _logger.LogInformation("The container '{container}' is created by migration tool, don't reset this one.", container.Name);

                        }
                        if (!isDmtGeneratedContainer && !string.IsNullOrEmpty(assetType))
                        {
                            properties?.Metadata?.Remove(MigrateResultKey);
                            properties?.Metadata?.Remove(AssetTypeKey);
                            properties?.Metadata?.Remove(OutputPathKey);
                            properties?.Metadata?.Remove(ManifestNameKey);
                            var deleteOperation = await container.SetMetadataAsync(properties?.Metadata, cancellationToken: cancellationToken);
                            if (deleteOperation.GetRawResponse().Status == 200)
                            {
                                _logger.LogInformation("Metadata in Container '{container}' is deleted successfully.", container.Name);
                                isReseted = true;
                            }
                            else
                            {
                                _logger.LogInformation("Metadata in Container '{container}' does not exist or was not deleted.", container.Name);

                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("An unexpected error occurred: {message}", ex.Message);

                }
            }
            return isReseted;
        }
    }
}
