using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    internal class AssetAnalyzer : BaseMigrator
    {
        private readonly AnalysisOptions _analysisOptions;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public AssetAnalyzer(
            GlobalOptions globalOptions,
            AnalysisOptions analysisOptions,
            IAnsiConsole console,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            TokenCredential credential,
            ILogger<AssetAnalyzer> logger)
            : base(globalOptions, console, credential, logger)
        {
            _analysisOptions = analysisOptions;
            _tracker = tracker;
        }

        private async Task<AnalysisResult> AnalyzeAsync<T>(T item, BlobServiceClient storage, CancellationToken cancellationToken)
        {
            string? assetName = null;
            string? containerName = null;
            BlobContainerClient? container = null;
            AsyncPageable<MediaAssetStreamingLocator>? locators = null;
            MediaAssetStorageEncryptionFormat? format = null;
            bool isForAmsAccount = false;
            if (item is MediaAssetResource mediaAsset)
            {
                assetName = mediaAsset.Data.Name;
                containerName = mediaAsset.Data.Container;
                format = mediaAsset.Data.StorageEncryptionFormat;
                container = storage.GetContainer(mediaAsset);
                locators = mediaAsset.GetStreamingLocatorsAsync();
                isForAmsAccount = true;
            }
            else if (item is BlobContainerItem bcItem)
            {
                assetName = storage.AccountName;
                container = storage.GetBlobContainerClient(bcItem.Name);
                containerName = container.Name;
                format = MediaAssetStorageEncryptionFormat.None;
            }
            else
            {
                throw new ArgumentException("item type is not supported.");
            }

            var result = new AnalysisResult(assetName, MigrationStatus.NotMigrated);
            _logger.LogDebug("Analyzing asset: {asset}, container: {container}", assetName, containerName);
            try
            {

                if (isForAmsAccount && !await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, assetName);
                    result.Status = MigrationStatus.Failed;
                    return result;
                }

                if (locators != null)
                {
                    await foreach (var locator in locators!)
                    {
                        if (locator.StreamingLocatorId != null && locator.StreamingLocatorId != Guid.Empty)
                        {
                            result.LocatorIds.Add(locator.StreamingLocatorId.Value.ToString("D"));
                        }
                    }
                }

                // The asset container exists, try to check the metadata list first.
                var migrateResult = await _tracker.GetMigrationStatusAsync(container, cancellationToken);

                if (migrateResult.Status != MigrationStatus.Completed && migrateResult.Status != MigrationStatus.Failed)
                {
                    // Do further check only when the Status in Metadata is not Completed nor Failed.

                    if (format != MediaAssetStorageEncryptionFormat.None)
                    {
                        _logger.LogWarning("Asset {name} is encrypted", assetName);

                        migrateResult.AssetType = AssetMigrationResult.AssetType_Encrypted;
                    }
                    else
                    {
                        var assetDetails = await container.GetDetailsAsync(_logger, cancellationToken, null, assetName, false);

                        if (assetDetails.Manifest == null)
                        {
                            migrateResult.AssetType = AssetMigrationResult.AssetType_NonIsm;
                        }
                        else
                        {
                            migrateResult.AssetType = assetDetails.Manifest.Format;
                            migrateResult.ManifestName = assetDetails.Manifest.FileName?.Replace(".ism", "");
                        }
                    }

                    if (!migrateResult.IsSupportedAsset())
                    {
                        migrateResult.Status = MigrationStatus.Skipped;
                    }
                }

                result.Status = migrateResult.Status;
                result.OutputPath = migrateResult.OutputPath;
                result.AssetType = migrateResult.AssetType;
                result.ManifestName = migrateResult.ManifestName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze asset {name}", assetName);
                result.Status = MigrationStatus.Failed;
            }
            _logger.LogDebug("Analyzed asset: {asset}, container: {container}, type: {type}, status: {status}", assetName, containerName, result.AssetType, result.Status);
            return result;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            _logger.LogInformation("Begin analysis of items for account: {name}", _analysisOptions.AccountName);
            var (isAMSAcc, account) = await IsAMSAccountAsync(_analysisOptions.AccountName, cancellationToken);
            var reportGenerator = new ReportGenerator(_globalOptions.HtmlReportFile, _globalOptions.JsonReportFile, _logger);
            reportGenerator.WriteHeader();
            var statistics = new AssetStats();
            var assetTypes = new ConcurrentDictionary<string, int>();
            if (!isAMSAcc)
            {
                var (storageClient, accountId) = await _resourceProvider.GetStorageAccount(_analysisOptions.AccountName, cancellationToken);
                if (storageClient == null)
                {
                    _logger.LogError("No valid storage account was found.");
                    throw new Exception("No valid storage account was found.");
                }
                double totalItems = await GetStorageBlobMetricAsync(accountId, cancellationToken);
                var containers = storageClient.GetBlobContainersAsync(
                              prefix: _analysisOptions.ResourceFilter, cancellationToken: cancellationToken);
                _logger.LogInformation("The total containers count of the storage account is {count}.", totalItems);
                List<BlobContainerItem>? filteredList = null;

                if (_analysisOptions.ResourceFilter != null)
                {
                    filteredList = await containers.ToListAsync();
                    totalItems = filteredList.Count;
                }
                _logger.LogInformation("The total containers to handle in this run is {count}.", totalItems);
                var channel = Channel.CreateBounded<double>(1);
                var progress = ShowProgressAsync("Analyzing Containers", "Assets", totalItems, channel.Reader, cancellationToken);
                var writer = channel.Writer;
                await MigrateInParallel(containers, filteredList, async (container, cancellationToken) =>
                {
                    //  var storage = await _resourceProvider.GetStorageAccountAsync(account, asset, cancellationToken);
                    var result = await AnalyzeAsync(container, storageClient, cancellationToken);
                    var assetType = result.AssetType ?? "unknown";
                    assetTypes.AddOrUpdate(assetType, 1, (key, value) => Interlocked.Increment(ref value));
                    reportGenerator?.WriteRecord(result);
                    statistics.Update(result);
                    await writer.WriteAsync(statistics.Total, cancellationToken);
                },
              _analysisOptions.BatchSize,
              cancellationToken);

                writer.Complete();
                await progress;
                _logger.LogDebug("Finished analysis of containers for account: {name}. Time taken {elapsed}", _analysisOptions.AccountName, watch.Elapsed);

            }
            else
            {
                if (account == null)
                {
                    _logger.LogError("No valid media account was found.");
                    throw new Exception("No valid media account was found.");
                }
                var resourceFilter = GetAssetResourceFilter(_analysisOptions.ResourceFilter,
                                                        _analysisOptions.CreationTimeStart,
                                                        _analysisOptions.CreationTimeEnd);

                double totalAssets = await QueryMetricAsync(account.Id.ToString(), "AssetCount", cancellationToken);
                _logger.LogInformation("The total asset count of the media account is {count}.", totalAssets);

                await _resourceProvider.SetStorageResourceGroupsAsync(account, cancellationToken);
                var assets = account.GetMediaAssets()
                    .GetAllAsync(resourceFilter, cancellationToken: cancellationToken);
                statistics = new AssetStats();
                assetTypes = new ConcurrentDictionary<string, int>();

                List<MediaAssetResource>? filteredList = null;

                if (resourceFilter != null)
                {
                    // When a filter is used, it usually include a small list of assets,
                    // The total count of asset can be extracted in advance without much perf hit.
                    filteredList = await assets.ToListAsync(cancellationToken);

                    totalAssets = filteredList.Count;
                }

                _logger.LogInformation("The total assets to handle in this run is {count}.", totalAssets);

                var channel = Channel.CreateBounded<double>(1);
                var progress = ShowProgressAsync("Analyzing Assets", "Assets", totalAssets, channel.Reader, cancellationToken);
                var writer = channel.Writer;
                await MigrateInParallel(assets, filteredList, async (asset, cancellationToken) =>
                {
                    var storage = await _resourceProvider.GetStorageAccountAsync(account, asset, cancellationToken);
                    var result = await AnalyzeAsync(asset, storage, cancellationToken);
                    var assetType = result.AssetType ?? "unknown";
                    assetTypes.AddOrUpdate(assetType, 1, (key, value) => Interlocked.Increment(ref value));
                    reportGenerator.WriteRecord(result);
                    statistics.Update(result);
                    await writer.WriteAsync(statistics.Total, cancellationToken);
                },
                _analysisOptions.BatchSize,
                cancellationToken);

                writer.Complete();
                await progress;
                _logger.LogDebug("Finished analysis of assets for account: {name}. Time taken {elapsed}", _analysisOptions.AccountName, watch.Elapsed);
            }

            WriteSummary(statistics, assetTypes);
            WriteDetails(assetTypes);

            reportGenerator.WriteTrailer();
            reportGenerator.Dispose();

        }


        private void WriteSummary(AssetStats statistics, IDictionary<string, int> assetTypes)
        {
            var table = new Table()
                .Title("[yellow]Asset Summary[/]")
                .HideHeaders()
                .AddColumn(string.Empty)
                .AddColumn(string.Empty)
                .AddRow("[yellow]Total[/]", $"{statistics.Total}")
                .AddRow("[darkgreen]Streamable[/]", $"{statistics.Streamable}")
                .AddRow("[green]Migrated[/]", $"{statistics.Migrated + statistics.Successful}")
                .AddRow("[red]Failed[/]", $"{statistics.Failed}")
                .AddRow("[darkorange]Skipped[/]", $"{statistics.Skipped}")
                .AddRow("[grey]No locators[/]", $"{statistics.NoLocators}");
            _console.Write(table);
        }

        private void WriteDetails(IDictionary<string, int> assetTypes)
        {
            var formats = new Table()
                .Title("[yellow]Asset Formats[/]")
                .HideHeaders()
                .AddColumn("Format")
                .AddColumn("Count");
            foreach (var (key, value) in assetTypes)
            {
                formats.AddRow($"[green]{key}[/]", $"[grey]{value}[/]");
            }
            _console.Write(formats);
        }
    }
}
