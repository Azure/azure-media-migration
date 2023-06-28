using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    internal class StorageMigrator : BaseMigrator
    {
        private readonly ILogger _logger;
        private readonly TransformFactory _transformFactory;
        private readonly StorageOptions _storageOptions;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public StorageMigrator(
            GlobalOptions options,
            StorageOptions storageOptions,
            IAnsiConsole console,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            TokenCredential credentials,
            TransformFactory transformFactory,
            ILogger<StorageMigrator> logger) :
            base(options, console, credentials)
        {
            _storageOptions = storageOptions;
            _tracker = tracker;
            _transformFactory = transformFactory;
            _logger = logger;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var (storageClient, accountId) = await _resourceProvider.GetStorageAccount(_storageOptions.AccountName, cancellationToken);
            _logger.LogInformation("Begin migration of containers from account: {name}", storageClient.AccountName);
            double totalContainers = await GetStorageBlobMetricAsync(accountId, cancellationToken);
            _logger.LogInformation("The total count of containers of the storage account is {count}.", totalContainers);

            var channel = Channel.CreateBounded<AssetStats>(1);
            var writer = channel.Writer;

            var containers = storageClient.GetBlobContainersAsync(
                               prefix: _storageOptions.Prefix ?? "asset-", cancellationToken: cancellationToken);

            List<BlobContainerItem>? filteredList = null;

            if (_storageOptions.Prefix != null)
            {
                // When a filter is used, it usually inlcude a small list of assets,
                // The accurate total count of containers can be extracted in advance without much perf hit.
                filteredList = await containers.ToListAsync();

                totalContainers = filteredList.Count;
            }

            _logger.LogInformation("The total input container to handle in this run is {count}.", totalContainers);

            //var progress = CreateProgressAsync("Migrate Containers", totalContainers, channel.Reader, cancellationToken);
            var progress = DisplayChartAsync(
                "Container Migration",
                totalContainers,
                channel.Reader);

            var stats = await MigrateAsync(storageClient, containers, filteredList, writer, cancellationToken);
            _logger.LogInformation("Finished migration of containers from account: {name}. Time : {time}", storageClient.AccountName, watch.Elapsed);
            await progress;

            //WriteSummary(totalContainers, stats);
        }

        public async Task DisplayChartAsync(
            string description,
            double maxValue,
            ChannelReader<AssetStats> stats)
        {
            var chart = new BarChart()
                .Width(60)
                .WithMaxValue(maxValue)
                .Label($"{description} ({maxValue})");
            await _console.Live(chart)
                .AutoClear(false)
                .StartAsync(async context =>
                {
                    chart.AddItems(GetItems(new AssetStats()));
                    await foreach(var value in stats.ReadAllAsync())
                    {
                        BarChartItem[] items = GetItems(value);
                        chart.Data.Clear();
                        chart.AddItems(items);
                        context.Refresh();
                    }
                });
        }

        private static BarChartItem[] GetItems(AssetStats value)
        {
            return new[]
            {
                new BarChartItem("Assets", value.Total),
                new BarChartItem("AlreadyMigrated", value.Migrated, Color.Green),
                new BarChartItem("Skipped", value.Skipped, Color.Grey),
                new BarChartItem("Successful", value.Successful, Color.Green),
                new BarChartItem("Failed", value.Failed, Color.Red),
                new BarChartItem("Deleted", value.Deleted, Color.Orange3)
            };
        }

        private async Task<MigrationResult> MigrateAsync(
            BlobServiceClient storageClient,
            BlobContainerItem container,
            CancellationToken cancellationToken)
        {
            var containerClient = storageClient.GetBlobContainerClient(container.Name);

            // Get the inital migration status from the container level's metadata list.
            var result = await _tracker.GetMigrationStatusAsync(containerClient, cancellationToken);

            // Check if already migrated.
            if (_storageOptions.SkipMigrated)
            {
                if (result.Status == MigrationStatus.Completed)
                {
                    _logger.LogDebug("Asset: {name} has already been migrated.", container.Name);
                    result.Status = MigrationStatus.AlreadyMigrated;
                    return result;
                }
            }

            var assetDetails = await containerClient.GetDetailsAsync(_logger, cancellationToken);            

            // AssetType and ManifestName are not supposed to change for a specific input asset,
            // Set AssetType and manifest from the input container before doing the actual transforming.
            if (assetDetails.Manifest != null)
            {
                result.AssetType = assetDetails.Manifest.Format;
                result.ManifestName = assetDetails.Manifest.FileName?.Replace(".ism", "");
            }
            else
            {
                result.AssetType = AssetMigrationResult.AssetType_NonIsm;
            }

            if (result.IsSupportedAsset)
            {
                var transforms = _transformFactory.StorageTransforms;

                foreach (var transform in transforms)
                {
                    var transformResult = await transform.RunAsync(assetDetails, cancellationToken);

                    result.Status = transformResult.Status;
                    result.OutputPath = transformResult.OutputPath;

                    if (result.Status == MigrationStatus.Failed)
                    {
                        break;
                    }
                }
            }
            else
            {
                // The asset type is not supported in this milestone,
                // Mark the status as Skipped for caller to do the statistics.
                result.Status = MigrationStatus.Skipped;
            }
            
            if (_storageOptions.MarkCompleted)
            {
                await _tracker.UpdateMigrationStatus(containerClient, result, cancellationToken);
            }
            if (result.Status == MigrationStatus.Completed && _storageOptions.DeleteMigrated)
            {
                _logger.LogWarning("Deleting container {name} after migration", container.Name);
                await storageClient.DeleteBlobContainerAsync(container.Name, cancellationToken: cancellationToken);
            }
            return result;
        }
        
        private async Task<AssetStats> MigrateAsync(
            BlobServiceClient storageClient,
            AsyncPageable<BlobContainerItem> containers,
            List<BlobContainerItem>? filteredList,
            ChannelWriter<AssetStats> writer,
            CancellationToken cancellationToken)
        {
            var stats = new AssetStats();

            await MigrateInBatches(containers, filteredList, async containers =>
            {
                var tasks = containers.Select(container => MigrateAsync(storageClient, container, cancellationToken));
                var results = await Task.WhenAll(tasks);
                stats.Total += results.Length;
                foreach (var result in results)
                {
                    switch (result.Status)
                    {
                        case MigrationStatus.Completed:
                            ++stats.Successful;
                            if (_storageOptions.DeleteMigrated)
                            {
                                ++stats.Deleted;
                            }
                            break;
                        case MigrationStatus.Skipped:
                            ++stats.Skipped;
                            break;
                        case MigrationStatus.AlreadyMigrated:
                            ++stats.Migrated;
                            break;
                        default:
                            ++stats.Failed;
                            break;
                    }
                }
                await writer.WriteAsync(stats, cancellationToken);
            },
            _storageOptions.BatchSize,
            cancellationToken);

            writer.Complete();
            return stats;
        }

        private void WriteSummary(double total, AssetStats stats)
        {
            var table = new Table()
                .AddColumn("Container Type")
                .AddColumn("Count")
                .AddRow("Total", $"{total}")
                .AddRow("Assets", $"{stats.Total}")
                .AddRow("[green]Already Migrated[/]", $"[green]{stats.Migrated}[/]")
                .AddRow("[gray]Skipped[/]", $"[gray]{stats.Skipped}[/]")
                .AddRow("[green]Successful[/]", $"[green]{stats.Successful}[/]")
                .AddRow("[red]Failed[/]", $"[red]{stats.Failed}[/]")
                .AddRow("[orange3]Deleted[/]", $"[orange3]{stats.Deleted}[/]");
            _console.Write(table);
        }
    }
}
