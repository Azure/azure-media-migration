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

            var containers = storageClient.GetBlobContainersAsync(
                               prefix: _storageOptions.Prefix, cancellationToken: cancellationToken);

            List<BlobContainerItem>? filteredList = null;

            if (_storageOptions.Prefix != null)
            {
                filteredList = await containers.ToListAsync();
                totalContainers = filteredList.Count;
            }

            _logger.LogInformation("The total input container to handle in this run is {count}.", totalContainers);

            var status = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Asset Migration", "Assets", totalContainers, status.Reader, cancellationToken);

            var stats = await MigrateAsync(storageClient, containers, filteredList, status.Writer, cancellationToken);
            _logger.LogInformation("Finished migration of containers from account: {name}. Time : {time}", storageClient.AccountName, watch.Elapsed);
            await progress;

            WriteSummary(totalContainers, stats);
        }

        private async Task<MigrationResult> MigrateAsync(
            BlobServiceClient storageClient,
            BlobContainerItem container,
            CancellationToken cancellationToken)
        {
            AssetMigrationResult result = new AssetMigrationResult(MigrationStatus.NotMigrated);
            _logger.LogInformation("Migrating asset: (container {container}) ...", container.Name);

            try
            {

                var containerClient = storageClient.GetBlobContainerClient(container.Name);

                // Get the initial migration status from the container level's metadata list.
                result = await _tracker.GetMigrationStatusAsync(containerClient, cancellationToken);

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate asset {name}.", container.Name);
                result.Status = MigrationStatus.Failed;
                _logger.LogDebug("Migrated asset: container: {container}, type: {type}, status: {status}", container.Name, result.AssetType, result.Status);
                return result;
            }

            return result;
        }

        private async Task<AssetStats> MigrateAsync(
            BlobServiceClient storageClient,
            AsyncPageable<BlobContainerItem> containers,
            List<BlobContainerItem>? filteredList,
            ChannelWriter<double> writer,
            CancellationToken cancellationToken)
        {
            var stats = new AssetStats();
            await MigrateInParallel(containers, filteredList, async (container, cancellationToken) =>
            {
                var result = await MigrateAsync(storageClient, container, cancellationToken);
                stats.Update(result);
                await writer.WriteAsync(stats.Total, cancellationToken);
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
