using AMSMigrate.Contracts;
using AMSMigrate.Transform;
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
        private readonly AssetOptions _assetOptions;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public StorageMigrator(
            GlobalOptions options,
            AssetOptions assetOptions,
            IAnsiConsole console,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            TokenCredential credentials,
            TransformFactory transformFactory,
            ILogger<StorageMigrator> logger) :
            base(options, console, credentials)
        {
            _assetOptions = assetOptions;
            _tracker = tracker;
            _transformFactory = transformFactory;
            _logger = logger;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var (storageClient, accountId) = await _resourceProvider.GetStorageAccount(cancellationToken);
            _logger.LogInformation("Begin migration of containers from account: {name}", storageClient.AccountName);
            double totalContainers = await GetStorageBlobMetricAsync(accountId, cancellationToken);

            var channel = Channel.CreateBounded<AssetStats>(1);
            var writer = channel.Writer;

            //var progress = CreateProgressAsync("Migrate Containers", totalContainers, channel.Reader, cancellationToken);
            var progress = DisplayChartAsync(
                "Container Migration",
                totalContainers,
                channel.Reader);
            var stats = await MigrateAsync(storageClient, writer, cancellationToken);
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
                new BarChartItem("Skiped", value.Skipped, Color.Grey),
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
            var result = new AssetMigrationResult();
            var containerClient = storageClient.GetBlobContainerClient(container.Name);
            // Check if already migrated.
            if (_assetOptions.SkipMigrated)
            {
                result = await _tracker.GetMigrationStatusAsync(containerClient, cancellationToken);
                if (result.Status == MigrationStatus.Success)
                {
                    _logger.LogDebug("Asset: {name} has already been migrated.", container.Name);
                    result.Status = MigrationStatus.AlreadyMigrated;
                    return result;
                }
            }

            var assetDetails = await containerClient.GetDetailsAsync(_logger, cancellationToken);
            var transforms = _transformFactory.StorageTransforms;
            var status = MigrationStatus.Skipped;
            foreach (var transform in transforms)
            {
                result = await transform.RunAsync(assetDetails, cancellationToken);

                if (result.Status != MigrationStatus.Skipped)
                    break;
            }
            
            if (_assetOptions.MarkCompleted)
            {
                await _tracker.UpdateMigrationStatus(containerClient, result, cancellationToken);
            }
            if (status == MigrationStatus.Success && _assetOptions.DeleteMigrated)
            {
                _logger.LogWarning("Deleting container {name} after migration", container.Name);
                await storageClient.DeleteBlobContainerAsync(container.Name, cancellationToken: cancellationToken);
            }
            return result;
        }
        
        private async Task<AssetStats> MigrateAsync(
            BlobServiceClient storageClient,
            ChannelWriter<AssetStats> writer,
            CancellationToken cancellationToken)
        {
            var stats = new AssetStats();
            var containers = storageClient.GetBlobContainersAsync(
                prefix: _globalOptions.ResourceFilter ?? "asset-", cancellationToken: cancellationToken);
            await MigrateInBatches(containers, async containers =>
            {
                var tasks = containers.Select(container => MigrateAsync(storageClient, container, cancellationToken));
                var results = await Task.WhenAll(tasks);
                stats.Total += results.Length;
                foreach (var result in results)
                {
                    switch (result.Status)
                    {
                        case MigrationStatus.Success:
                            ++stats.Successful;
                            if (_assetOptions.DeleteMigrated)
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
            _assetOptions.BatchSize,
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
                .AddRow("[gray]Skiped[/]", $"[gray]{stats.Skipped}[/]")
                .AddRow("[green]Successful[/]", $"[green]{stats.Successful}[/]")
                .AddRow("[red]Failed[/]", $"[red]{stats.Failed}[/]")
                .AddRow("[orange3]Deleted[/]", $"[orange3]{stats.Deleted}[/]");
            _console.Write(table);
        }
    }
}
