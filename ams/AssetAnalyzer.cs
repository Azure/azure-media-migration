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
        private readonly AnalyzeTransform _analyzer;
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
            _analyzer = new AnalyzeTransform(tracker, logger);
        }

        private async Task<AnalysisResult> AnalyzeAsync(MediaAssetResource asset, BlobServiceClient storage, CancellationToken cancellationToken)
        {
            var result = new AnalysisResult(asset.Data.Name, null, 0, MigrationStatus.Skipped);
            _logger.LogDebug("Analyzing asset: {asset}", asset.Data.Name);
            try
            {
                var container = storage.GetContainer(asset);
                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                    result.Status = MigrationStatus.Failure;
                    return result;
                }

                if (_analysisOptions.AnalysisType == AnalysisType.Detailed)
                {
                    if (asset.Data.StorageEncryptionFormat != MediaAssetStorageEncryptionFormat.None)
                    {
                        _logger.LogWarning("Skipping asset {name} as it is encrypted", asset.Data.Name);
                        return result;
                    }
                    var assetDetails = await container.GetDetailsAsync(_logger, cancellationToken, asset.Data.Name, false);
                    return await _analyzer.RunAsync(assetDetails, cancellationToken);
                }
                else
                {
                    if (await container.ExistsAsync(cancellationToken))
                    {

                    }
                    var status = await _tracker.GetMigrationStatusAsync(container, cancellationToken);
                    result.Status = status.Status;
                    result.Uri = status.Uri;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze asset {name}", asset.Data.Name);
                result.Status = MigrationStatus.Failure;
            }
            return result;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            _logger.LogInformation("Begin analysis of assets for account: {name}", _globalOptions.AccountName);
            var account = await GetMediaAccountAsync(cancellationToken);
            double totalAssets = await QueryMetricAsync(account.Id.ToString(), "AssetCount", cancellationToken);
            var storage = await _resourceProvider.GetStorageAccountAsync(account, cancellationToken);
            ReportGenerator? reportGenerator = null;

            if (_analysisOptions.AnalysisType == AnalysisType.Report)
            {
                var file = File.OpenWrite(Path.Combine(_globalOptions.LogDirectory, $"Report_{DateTime.Now:hh-mm-ss}.html"));
                reportGenerator = new ReportGenerator(file);
                reportGenerator.WriteHeader();
            }
            var assets = account.GetMediaAssets()
                .GetAllAsync(_globalOptions.ResourceFilter, cancellationToken: cancellationToken);
            var statistics = new Statistics();
            var assetTypes = new SortedDictionary<string, int>();

            var channel = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Analyzing Assets", "Assets", totalAssets, channel.Reader, cancellationToken);
            var writer = channel.Writer;
            await MigrateInBatches(assets, async assets =>
            {
                var tasks = assets.Select(async asset =>
                {
                    return await AnalyzeAsync(asset, storage, cancellationToken);
                });
                var results = await Task.WhenAll(tasks);
                reportGenerator?.WriteRows(results);
                statistics.TotalAssets += assets.Length;
                statistics.Migrated += results.Count(r => r.Status == MigrationStatus.Success);
                statistics.Failed += results.Count(r => r.Status == MigrationStatus.Failure);
                statistics.Skipped += results.Count(r => r.Status == MigrationStatus.Skipped);
                await writer.WriteAsync(statistics.TotalAssets, cancellationToken);
                foreach (var result in results)
                {
                    if (result == null) continue;
                    if (result.Format != null)
                    {
                        statistics.StreamableAssets++;
                    }

                    var format = result.Format ?? "unknown";
                    if (assetTypes.ContainsKey(format))
                    {
                        assetTypes[format] += 1;
                    }
                    else
                    {
                        assetTypes.Add(format, 1);
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
            _logger.LogDebug("Finished analysis of assets for account: {name}. Time taken {elapsed}", _globalOptions.AccountName, watch.Elapsed);
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
