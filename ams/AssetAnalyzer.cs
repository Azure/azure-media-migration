using AMSMigrate.Azure;
using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    record struct Statistics(
        int TotalAssets,
        int StreamableAssets,
        int Migrated,
        int Failed,
        int Skipped,
        int NoLocators);

    internal class AssetAnalyzer : BaseMigrator
    {
        private readonly ILogger _logger;
        private readonly AnalysisOptions _analysisOptions;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public AssetAnalyzer(
            GlobalOptions globalOptions,
            AnalysisOptions analysisOptions,
            IAnsiConsole console,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            TokenCredential credential,
            ILogger<AssetAnalyzer> logger)
            : base(globalOptions, console, credential)
        {
            _analysisOptions = analysisOptions;
            _tracker = tracker;
            _logger = logger;
        }

        private async Task<AnalysisResult> AnalyzeAsync(MediaAssetResource asset, BlobServiceClient storage, CancellationToken cancellationToken)
        {
            var result = new AnalysisResult(asset.Data.Name, MigrationStatus.NotMigrated, 0);
            _logger.LogDebug("Analyzing asset: {asset} (container {container})", asset.Data.Name, asset.Data.Container);
            try
            {
                var container = storage.GetContainer(asset);
                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                    result.Status = MigrationStatus.Failed;
                    return result;
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
                        var assetDetails = await container.GetDetailsAsync(_logger, cancellationToken, asset.Data.Name, false);

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

                    if (!migrateResult.IsSupportedAsset)
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
            return result;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            _logger.LogInformation("Begin analysis of assets for account: {name}", _analysisOptions.AccountName);
            var account = await GetMediaAccountAsync(_analysisOptions.AccountName, cancellationToken);
            double totalAssets = await QueryMetricAsync(account.Id.ToString(), "AssetCount", cancellationToken);
            _logger.LogInformation("The total asset count of the media account is {count}.", totalAssets);

            var storage = await _resourceProvider.GetStorageAccountAsync(account, cancellationToken);
            ReportGenerator? reportGenerator = null;

            if (_analysisOptions.AnalysisType == AnalysisType.Report)
            {
                var file = File.OpenWrite(Path.Combine(_globalOptions.LogDirectory, $"Report_{DateTime.Now:hh-mm-ss}.html"));
                reportGenerator = new ReportGenerator(file);
                reportGenerator.WriteHeader();
            }
            var assets = account.GetMediaAssets()
                .GetAllAsync(_analysisOptions.ResourceFilter, cancellationToken: cancellationToken);
            var statistics = new Statistics();
            var assetTypes = new SortedDictionary<string, int>();

            List<MediaAssetResource>? filteredList = null;

            if (_analysisOptions.ResourceFilter != null)
            {
                // When a filter is used, it usually inlcude a small list of assets,
                // The total count of asset can be extracted in advance without much perf hit.
                filteredList = await assets.ToListAsync(cancellationToken);

                totalAssets = filteredList.Count;
            }

            _logger.LogInformation("The total assets to handle in this run is {count}.", totalAssets);

            var channel = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Analyzing Assets", "Assets", totalAssets, channel.Reader, cancellationToken);
            var writer = channel.Writer;
            await MigrateInBatches(assets, filteredList, async assets =>
            {
                var tasks = assets.Select(async asset =>
                {
                    return await AnalyzeAsync(asset, storage, cancellationToken);
                });
                var results = await Task.WhenAll(tasks);
                reportGenerator?.WriteRows(results);
                statistics.TotalAssets += assets.Length;
                statistics.Migrated += results.Count(r => r.Status == MigrationStatus.Completed);
                statistics.Failed += results.Count(r => r.Status == MigrationStatus.Failed);
                statistics.Skipped += results.Count(r => r.Status == MigrationStatus.Skipped);
                await writer.WriteAsync(statistics.TotalAssets, cancellationToken);
                foreach (var result in results)
                {
                    if (result == null) continue;
                    if (result.IsStreamable)
                    {
                        statistics.StreamableAssets++;
                    }

                    var assetType = result.AssetType ?? "unknown";
                    if (assetTypes.ContainsKey(assetType))
                    {
                        assetTypes[assetType] += 1;
                    }
                    else
                    {
                        assetTypes.Add(assetType, 1);
                    }
                    if (result.Locators == 0)
                    {
                        statistics.NoLocators++;
                    }
                }
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
            reportGenerator?.Dispose();
        }

        private void WriteSummary(Statistics statistics, IDictionary<string, int> assetTypes)
        {
            var table = new Table()
                .Title("[yellow]Asset Summary[/]")
                .HideHeaders()
                .AddColumn(string.Empty)
                .AddColumn(string.Empty)
                .AddRow("[yellow]Total[/]", $"{statistics.TotalAssets}")
                .AddRow("[darkgreen]Streamable[/]", $"{statistics.StreamableAssets}")
                .AddRow("[green]Migrated[/]", $"{statistics.Migrated}")
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
