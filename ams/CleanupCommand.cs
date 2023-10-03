using AMSMigrate.Contracts;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AMSMigrate.Ams
{
    internal class CleanupCommand : BaseMigrator
    {
        private readonly CleanupOptions _options;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public CleanupCommand(GlobalOptions globalOptions,
            CleanupOptions cleanupOptions,
            IAnsiConsole console,
            TokenCredential credential,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            ILogger<CleanupCommand> logger)
            : base(globalOptions, console, credential, logger)
        {
            _options = cleanupOptions;
            _tracker = tracker;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {

            var (isAMSAcc, account) = await IsAMSAccountAsync(_options.AccountName, cancellationToken);
            if (!isAMSAcc || account == null)
            {
                _logger.LogInformation("No valid media account was found.");
                throw new Exception("No valid media account was found.");
            }
            _logger.LogInformation("Begin cleaning up on account: {name}", account.Data.Name);

            if (_options.IsCleanUpAccount)
            {
                Console.Write($"Do you want to delete the account '{account.Data.Name}'? (y/n): ");
                string? userResponse = Console.ReadLine();

                if (!(userResponse?.ToLower() == "y"))
                {
                    Console.WriteLine("Account cleanup canceled by user.");
                    return;
                }
            }

            Dictionary<string, bool> stats = new Dictionary<string, bool>();
            AsyncPageable<MediaAssetResource> assets;

            //clean up asset
            var resourceFilter = _options.IsCleanUpAccount ? null : GetAssetResourceFilter(_options.ResourceFilter, null, null);

            var orderBy = "properties/created";
            await _resourceProvider.SetStorageResourceGroupsAsync(account, cancellationToken);
            assets = account.GetMediaAssets()
                .GetAllAsync(resourceFilter, orderby: orderBy, cancellationToken: cancellationToken);
            List<MediaAssetResource>? assetList = await assets.ToListAsync(cancellationToken);

            foreach (var asset in assetList)
            {
                var result = await CleanUpAssetAsync(_options.IsCleanUpAccount || _options.IsForceCleanUpAsset, account, asset, cancellationToken);
                stats.Add(asset.Data.Name, result);
            }
            WriteSummary(stats, false);

            if (_options.IsCleanUpAccount)
            {
                Dictionary<string, bool> accStats = new Dictionary<string, bool>();
                var result = await CleanUpAccountAsync(account, cancellationToken);
                accStats.Add(account.Data.Name, result);
                WriteSummary(accStats, true);
            }

        }

        private void WriteSummary(IDictionary<string, bool> stats, bool isDeletingAccount)
        {
            var table = new Table();
            if (isDeletingAccount)
            {
                table.AddColumn("Account");
            }
            else
            {
                table.AddColumn("Asset");
            }
            table.AddColumn("IsDeleted");
            foreach (var (key, value) in stats)
            {
                var status = value ? $"[green]{value}[/]" : $"[red]{value}[/]";
                table.AddRow($"[green]{key}[/]", status);
            }

            _console.Write(table);
        }
        private async Task<bool> CleanUpAccountAsync(MediaServicesAccountResource account, CancellationToken cancellationToken)
        {
            try
            {
                var endpoints = account.GetStreamingEndpoints();
                var liveevents = account.GetMediaLiveEvents();
                var policies = account.GetContentKeyPolicies();

                if (endpoints != null)
                {
                    foreach (var streamingEndpoint in endpoints)
                    {
                        if (streamingEndpoint.Data.ResourceState == StreamingEndpointResourceState.Running)
                        {
                            await streamingEndpoint.StopAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                        }
                        await streamingEndpoint.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                    }
                }
                if (policies != null)
                {
                    foreach (var contentKeyPolicy in policies)
                    {
                        await contentKeyPolicy.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                    }
                }
                if (liveevents != null)
                {
                    foreach (var liveEvent in liveevents)
                    {
                        if (liveEvent.Data.ResourceState == LiveEventResourceState.Running)
                        {
                            await liveEvent.StopAsync(WaitUntil.Completed, new LiveEventActionContent() { RemoveOutputsOnStop = true }, cancellationToken: cancellationToken);
                        }
                        await liveEvent.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                    }
                }

                var deleteOperation = await account.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);

                if (deleteOperation.HasCompleted && deleteOperation.GetRawResponse().Status == 200)
                {
                    _logger.LogInformation("The media account {account} has been deleted.", account.Data.Name);
                    return true;
                }
                else
                {
                    _logger.LogInformation("The media account {account} deletion failed.", account.Data.Name);
                    return false;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete account {name}", account.Data.Name);
                return false;
            }
        }
        private async Task<bool> CleanUpAssetAsync(bool isForcedelete, MediaServicesAccountResource account, MediaAssetResource asset, CancellationToken cancellationToken)
        {
            try
            {

                var storage = await _resourceProvider.GetStorageAccountAsync(account, asset, cancellationToken);
                var container = storage.GetContainer(asset);
                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                    return false;
                }
                // The asset container exists, try to check the metadata list first.

                if (isForcedelete || (_tracker.GetMigrationStatusAsync(container, cancellationToken).Result.Status == MigrationStatus.Completed))
                {
                    var locator = await account.GetStreamingLocatorAsync(asset, cancellationToken);
                    if (locator != null)
                    {
                        await locator.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                    }

                    if (asset != null)
                    {
                        await asset.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                    }
                    await container.DeleteAsync(cancellationToken: cancellationToken);
                    _logger.LogDebug("locator: {locator}, Migrated asset: {asset} , container: {container} are deleted.", locator?.Data.Name, asset?.Data.Name, container?.Name);
                    return true;
                }
                else
                {
                    _logger.LogDebug("asset: {asset} does not meet the criteria for deletion.", asset.Data.Name);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete asset {name}", asset.Data.Name);
                return false;
            }
        }
    }
}
