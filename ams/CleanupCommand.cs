using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AMSMigrate.ams
{
    internal class CleanupCommand : BaseMigrator
    {
        private readonly ILogger _logger;
        private readonly CleanupOptions _options;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public CleanupCommand(GlobalOptions globalOptions,
            CleanupOptions cleanupOptions,
            IAnsiConsole console,
            TokenCredential credential,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            ILogger<CleanupCommand> logger)
            : base(globalOptions, console, credential)
        {
            _options = cleanupOptions;
            _logger = logger;
            _tracker = tracker;
        }
        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var account = await GetMediaAccountAsync(_options.AccountName, cancellationToken);
            _logger.LogInformation("Begin cleaning up on account: {name}", account.Data.Name);
            Dictionary<string, bool> stats = new Dictionary<string, bool>();

            var totalAssets = await QueryMetricAsync(
                account.Id.ToString(),
                "AssetCount",
                cancellationToken: cancellationToken);

            _logger.LogInformation("The total asset count of the media account is {count}.", totalAssets);
            AsyncPageable<MediaAssetResource> assets;
            if (!_options.IsCleanUpAccount)
            {
                var resourceFilter = GetAssetResourceFilter(_options.ResourceFilter, null, null);

                var orderBy = "properties/created";
                assets = account.GetMediaAssets()
                    .GetAllAsync(resourceFilter, orderby: orderBy, cancellationToken: cancellationToken);
            }
            else
            {
                assets = account.GetMediaAssets()
                   .GetAllAsync();
            }
            List<MediaAssetResource>? assetList = await assets.ToListAsync(cancellationToken);

            foreach (var asset in assetList)
            {
                var result = await CleanUpAssetAsync(account, asset, cancellationToken);
                stats.Add(asset.Data.Name, result);
            }
            WriteSummary(stats, false);
            if (_options.IsCleanUpAccount)
            {
                Console.Write($"Do you want to delete the account '{account.Data.Name}'? (y/n): ");
                string? userResponse = Console.ReadLine();

                if (userResponse?.ToLower() == "y")
                {
                    Dictionary<string, bool> accStats = new Dictionary<string, bool>();
                    var result = await CleanUpAccountAsync(account, cancellationToken);
                    accStats.Add(account.Data.Name, result);
                    WriteSummary(accStats, true);
                }
                else
                {
                    Console.WriteLine("Account cleanup canceled by user.");
                }
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

                var deleteOperation = await account.DeleteAsync(WaitUntil.Completed);

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
        private async Task<bool> CleanUpAssetAsync(MediaServicesAccountResource account, MediaAssetResource asset, CancellationToken cancellationToken)
        {
            try
            {

                var storage = await _resourceProvider.GetStorageAccountAsync(account, cancellationToken);
                var container = storage.GetContainer(asset);
                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);

                    return false;
                }

                // The asset container exists, try to check the metadata list first.
                var migrateResult = await _tracker.GetMigrationStatusAsync(container, cancellationToken);

                if (_options.IsCleanUpAccount || _options.IsForceCleanUpAsset || migrateResult.Status == MigrationStatus.Completed)
                {
                    var policies = account.GetContentKeyPolicies();
                    var locator = await account.GetStreamingLocatorAsync(asset, cancellationToken);
                    if (_options.IsCleanUpAccount)
                    {
                        var endpoints = account.GetStreamingEndpoints();
                        var liveevents = account.GetMediaLiveEvents();
                        await CleanUpContentAsync(container, asset, locator, endpoints, policies, liveevents);
                        _logger.LogDebug("account {account} is deleted.", account.Data.Name);

                    }
                    else
                    {
                        await CleanUpContentAsync(container, asset, locator, null, policies, null);
                        _logger.LogDebug("locator: {locator}, Migrated asset: {asset} , container: {container} are deleted.", locator?.Data.Name, asset.Data.Name, container?.Name);
                    }
                    return true;
                }
                else
                {
                    _logger.LogDebug("asset: {asset} with migration status: {migrateResult} does not meet the criteria for deletion.", asset.Data.Name, migrateResult.Status);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete asset {name}", asset.Data.Name);
                return false;
            }
        }

        /// <summary>
        /// Delete the resources that were created.
        /// </summary>
        /// <param name="inputAssets">The input Asset List.</param>
        /// <param name="streamingLocator">The streaming locator. </param>
        /// <returns>A task.</returns>
        private async Task CleanUpContentAsync(
            BlobContainerClient container,
            MediaAssetResource? inputAsset,
            StreamingLocatorResource? streamingLocator,
            StreamingEndpointCollection? streamingEndpoints,
            ContentKeyPolicyCollection? contentKeyPolicies,
            MediaLiveEventCollection? liveEvents)
        {

            if (streamingLocator != null)
            {
                await streamingLocator.DeleteAsync(WaitUntil.Completed);
            }

            if (inputAsset != null)
            {
                await inputAsset.DeleteAsync(WaitUntil.Completed);
            }

            if (streamingEndpoints != null)
            {
                foreach (var streamingEndpoint in streamingEndpoints)
                {
                    await streamingEndpoint.DeleteAsync(WaitUntil.Completed);
                }
            }
            if (contentKeyPolicies != null)
            {
                foreach (var contentKeyPolicy in contentKeyPolicies)
                {
                    await contentKeyPolicy.DeleteAsync(WaitUntil.Completed);
                }
            }
            if (liveEvents != null)
            {
                foreach (var liveEvent in liveEvents)
                {
                    await liveEvent.DeleteAsync(WaitUntil.Completed);
                }
            }
            await container.DeleteAsync();
        }
    }
}
