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
            base(options, console, credentials, logger)
        {
            _storageOptions = storageOptions;
            _tracker = tracker;
            _transformFactory = transformFactory;
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
                               prefix: _storageOptions.Prefix, cancellationToken: cancellationToken);

            List<BlobContainerItem>? filteredList = null;

            if (_storageOptions.Prefix != null)
            {
                // When a filter is used, it usually include a small list of assets,
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
                    await foreach (var value in stats.ReadAllAsync())
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
                new BarChartItem("Failed", value.Failed, Color.Red)
            };
        }

        private async Task<MigrationResult> MigrateAsync(
            BlobServiceClient storageClient,
            BlobContainerItem container,
            CancellationToken cancellationToken)
        {
            var containerClient = storageClient.GetBlobContainerClient(container.Name);

            // Get the initial migration status from the container level's metadata list.
            var result = await _tracker.GetMigrationStatusAsync(containerClient, cancellationToken);

            // Check if already migrated.
            if (_storageOptions.SkipMigrated)
            {
                if (result.Status == MigrationStatus.Completed)
                {
                    _logger.LogDebug("Container: {name} has already been migrated.", container.Name);
                    result.Status = MigrationStatus.AlreadyMigrated;
                    return result;
                }
            }

            if (result.AssetType == AssetMigrationResult.AssetType_DmtGenerated)
            {
                _logger.LogDebug("Container: {name} holds the migrated data.", container.Name);
                result.Status = MigrationStatus.Skipped;
                return result;
            }

            var assetDetails = await containerClient.GetDetailsAsync(_logger, cancellationToken, _storageOptions.OutputManifest);

            // AssetType and ManifestName are not supposed to change for a specific input asset,
            // Set AssetType and manifest from the input container before doing the actual transforming.
            if (assetDetails.Manifest != null)
            {
                result.AssetType = assetDetails.Manifest.Format;
                result.ManifestName = _storageOptions.OutputManifest ?? assetDetails.Manifest.FileName?.Replace(".ism", "");
            }
            else
            {
                result.AssetType = AssetMigrationResult.AssetType_NonIsm;
            }

            if (result.IsSupportedAsset())
            {
                var uploader = _transformFactory.GetUploader(_storageOptions);
                var (Container, Path) = _transformFactory.TemplateMapper.ExpandPathTemplate(
                                                    assetDetails.Container,
                                                    _storageOptions.PathTemplate);

                var canUpload = await uploader.CanUploadAsync(
                                                    Container,
                                                    Path,
                                                    cancellationToken);
                if (canUpload)
                {
                    try
                    {
                        var transforms = _transformFactory.GetTransforms(_globalOptions, _storageOptions);

                        foreach (var transform in transforms)
                        {
                            var transformResult = await transform.RunAsync(assetDetails, cancellationToken);

                            result.Status = transformResult.Status;
                            result.OutputPath = transformResult.OutputPath;

                            if (result.Status != MigrationStatus.Skipped)
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        await uploader.UploadCleanupAsync(Container, Path, cancellationToken);
                    }
                }
                else
                {
                    //
                    // Another instance of the tool is working on the output container,
                    //
                    result.Status = MigrationStatus.Skipped;

                    _logger.LogWarning("Another instance of tool is working on the container {container} and output path: {output}",
                                       Container,
                                       Path);
                }
            }
            else
            {
                // The asset type is not supported in this milestone,
                // Mark the status as Skipped for caller to do the statistics.
                result.Status = MigrationStatus.Skipped;

                _logger.LogWarning("Skipping container {name} because it is not in a supported format!!!", container.Name);
            }

            await _tracker.UpdateMigrationStatus(containerClient, result, cancellationToken);

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
            await MigrateInParallel(containers, filteredList, async (container, cancellationToken) =>
            {
                var result = await MigrateAsync(storageClient, container, cancellationToken);
                stats.Update(result);
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
                .AddRow("[red]Failed[/]", $"[red]{stats.Failed}[/]");

            _console.Write(table);
        }
    }
}
