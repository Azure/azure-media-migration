using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Extensions.Msal;
using Spectre.Console;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public override Task MigrateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<object>(null);
        }

        public async Task CleanUpAsync(CancellationToken cancellationToken)
        {
            if (_options.CleanupType == CleanupType.AMSAccount)
            {
                var account = await GetMediaAccountAsync(_options.AccountName, cancellationToken);
                Dictionary<string, bool> stats = new Dictionary<string, bool>();

                if (_options.IsCleanUpAccount)
                {
                    var result = await CleanUpAccountAsync(account, cancellationToken);
                    stats.Add(account.Data.Name, result);
                    WriteSummary(stats, true);
                    return;
                }
                else
                {
                    var totalAssets = await QueryMetricAsync(
                        account.Id.ToString(),
                    "AssetCount",
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("The total asset count of the media account is {count}.", totalAssets);

                    var resourceFilter = GetAssetResourceFilter(_options.ResourceFilter, null, null);

                    var orderBy = "properties/created";
                    var assets = account.GetMediaAssets()
                        .GetAllAsync(resourceFilter, orderby: orderBy, cancellationToken: cancellationToken);

                    List<MediaAssetResource>? filteredList = await assets.ToListAsync(cancellationToken);

                    foreach (var asset in filteredList)
                    {
                        var result = await CleanUpAssetAsync(account, asset, cancellationToken);
                        stats.Add(asset.Data.Name, result);
                    }
                    WriteSummary(stats, false);
                }
            }
            else
            {
                _logger.LogInformation("Skipping the deletion step - asset type {name}", _options.CleanupType.ToString());
            }         
        }

        private void WriteSummary(IDictionary<string, bool> stats,bool isDeletingAccount)
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
        {   try
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

                if (_options.IsForceCleanUpAsset || migrateResult.Status == MigrationStatus.Completed || migrateResult.Status == MigrationStatus.AlreadyMigrated)
                {
                    var locator = account.GetStreamingLocatorAsync(asset, cancellationToken).Result;
                    await CleanUpContentAsync(container, asset, locator);
                    _logger.LogDebug("locator: {locator}, Migrated asset: {asset} , container: {container} are deleted.", asset.Data.Name, container?.Name, locator?.Data.Name);
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
            StreamingLocatorResource? streamingLocator)
        {
            
            if (streamingLocator != null)
            {
                await streamingLocator.DeleteAsync(WaitUntil.Completed);
            }

            if (inputAsset != null)
            {
                await inputAsset.DeleteAsync(WaitUntil.Completed);
            }
        
            await container.DeleteAsync();
        }
    }
}
