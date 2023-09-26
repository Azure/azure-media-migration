using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        private async Task<AnalysisResult> AnalyzeAsync(MediaAssetResource asset, BlobServiceClient storage, CancellationToken cancellationToken)
        {
            var result = new AnalysisResult(asset.Data.Name, MigrationStatus.NotMigrated);
            _logger.LogDebug("Analyzing asset: {asset}, container: {container}", asset.Data.Name, asset.Data.Container);
            try
            {
                var container = storage.GetContainer(asset);
                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                    result.Status = MigrationStatus.Failed;
                    return result;
                }

                // Get a list of LocatorIds if they exist.
                var locators = asset.GetStreamingLocatorsAsync();

                await foreach (var locator in locators)
                {
                    if (locator.StreamingLocatorId != null && locator.StreamingLocatorId != Guid.Empty)
                    {
                        result.LocatorIds.Add(locator.StreamingLocatorId.Value.ToString("D"));
                    }                    
                }

                // The asset container exists, try to check the metadata list first.
                var migrateResult = await _tracker.GetMigrationStatusAsync(container, cancellationToken);

                if (migrateResult.Status != MigrationStatus.Completed && migrateResult.Status != MigrationStatus.Failed)
                {
                    // Do further check only when the Status in Metadata is not Completed nor Failed.

                    if (asset.Data.StorageEncryptionFormat != MediaAssetStorageEncryptionFormat.None)
                    {
                        _logger.LogWarning("Asset {name} is encrypted", asset.Data.Name);

                        migrateResult.AssetType = AssetMigrationResult.AssetType_Encrypted;
                    }
                    else
                    {
                        var assetDetails = await container.GetDetailsAsync(_logger, cancellationToken, null, asset.Data.Name, false);

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
                _logger.LogError(ex, "Failed to analyze asset {name}", asset.Data.Name);
                result.Status = MigrationStatus.Failed;
            }
            _logger.LogDebug("Analyzed asset: {asset}, container: {container}, type: {type}, status: {status}", asset.Data.Name, asset.Data.Container, result.AssetType, result.Status);
            return result;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            _logger.LogInformation("Begin analysis of assets for account: {name}", _analysisOptions.AccountName);
            var account = await GetMediaAccountAsync(_analysisOptions.AccountName, cancellationToken);
            double totalAssets = await QueryMetricAsync(account.Id.ToString(), "AssetCount", cancellationToken);
            _logger.LogInformation("The total asset count of the media account is {count}.", totalAssets);

            ReportGenerator? reportGenerator = null;

            var resourceFilter = GetAssetResourceFilter(_analysisOptions.ResourceFilter,
                                                        _analysisOptions.CreationTimeStart,
                                                        _analysisOptions.CreationTimeEnd);

            if (_analysisOptions.AnalysisType == AnalysisType.Report)
            {
                _logger.LogDebug("Writing html report to {file}", _globalOptions.ReportFile);
                var file = File.OpenWrite(_globalOptions.ReportFile);
                reportGenerator = new ReportGenerator(file);
                reportGenerator.WriteHeader();
            }
            await _resourceProvider.SetStorageResourceGroupsAsync(account, cancellationToken);
            var assets = account.GetMediaAssets()
                .GetAllAsync(resourceFilter, cancellationToken: cancellationToken);
            var statistics = new AssetStats();
            var assetTypes = new ConcurrentDictionary<string, int>();

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
                reportGenerator?.WriteRow(result);
                statistics.Update(result);
                await writer.WriteAsync(statistics.Total, cancellationToken);
            },
            _analysisOptions.BatchSize,
            cancellationToken);

            writer.Complete();
            await progress;
            _logger.LogDebug("Finished analysis of assets for account: {name}. Time taken {elapsed}", _analysisOptions.AccountName, watch.Elapsed);
            WriteSummary(statistics, assetTypes);
            if (_analysisOptions.AnalysisType == AnalysisType.Detailed)
            {
                WriteDetails(assetTypes);
            }

            if (_analysisOptions.AnalysisType == AnalysisType.Report)
            {
                reportGenerator?.WriteTrailer();
                reportGenerator?.Dispose();
                _logger.LogInformation("See file {file} for detailed html report.", _globalOptions.ReportFile);
            }
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
