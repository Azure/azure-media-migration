using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    internal class AssetMigrator : BaseMigrator
    {
        private readonly TransformFactory _transformFactory;
        private readonly AssetOptions _options;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public AssetMigrator(
            GlobalOptions globalOptions,
            AssetOptions assetOptions,
            IAnsiConsole console,
            TokenCredential credential,
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            ILogger<AssetMigrator> logger,
            TransformFactory transformFactory) :
            base(globalOptions, console, credential, logger)
        {
            _options = assetOptions;
            _tracker = tracker;
            _transformFactory = transformFactory;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var (isAMSAcc, account) = await IsAMSAccountAsync(_options.AccountName, cancellationToken);
            if (!isAMSAcc || account == null)
            {
                _logger.LogInformation("No valid media account was found.");
                throw new Exception("No valid media account was found.");
            }
            _logger.LogInformation("Begin migration of assets for account: {name}", account.Data.Name);
            var totalAssets = await QueryMetricAsync(
                account.Id.ToString(),
                "AssetCount",
                cancellationToken: cancellationToken);

            _logger.LogInformation("The total asset count of the media account is {count}.", totalAssets);

            var resourceFilter = GetAssetResourceFilter(_options.ResourceFilter,
                                                        _options.CreationTimeStart,
                                                        _options.CreationTimeEnd);

            var orderBy = "properties/created";

            await _resourceProvider.SetStorageAccountResourcesAsync(account, cancellationToken);
            var assets = account.GetMediaAssets().GetAllAsync(resourceFilter, orderby: orderBy, cancellationToken: cancellationToken);

            List<MediaAssetResource>? filteredList = null;

            if (resourceFilter != null)
            {
                // When a filter is used, it usually include a small list of assets,
                // The accurate total count of asset can be extracted in advance without much perf hit.
                filteredList = await assets.ToListAsync(cancellationToken);

                totalAssets = filteredList.Count;
            }

            if (_options.AssetIdListFile is not null)
            {
                // Read asset id file and convert to list of GUIDs
                string[] assetIds = await File.ReadAllLinesAsync(_options.AssetIdListFile, cancellationToken);
                Guid[] assetGuids = assetIds.Select(assetId =>
                {
                    if (Guid.TryParse(assetId, out Guid assetGuid))
                    {
                        return (Guid?)assetGuid;
                    }

                    return null;
                }).Where(x => x.HasValue).Select(x => x.Value).ToArray();

                // Build filtered list
                filteredList = await assets.Where(x => x.Data.AssetId.HasValue && assetGuids.Contains(x.Data.AssetId.Value)).ToListAsync(cancellationToken);
                totalAssets = filteredList.Count;
            }

            _logger.LogInformation("The total assets to handle in this run is {count}.", totalAssets);

            var status = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Asset Migration", "Assets", totalAssets, status.Reader, cancellationToken);

            var stats = await MigrateAsync(account, assets, filteredList, status.Writer, cancellationToken);
            _logger.LogInformation("Finished migration of assets for account: {name}. Time taken: {time}", account.Data.Name, watch.Elapsed);
            await progress;
            WriteSummary(stats);
        }

        private async Task<AssetStats> MigrateAsync(MediaServicesAccountResource account, AsyncPageable<MediaAssetResource> assets, List<MediaAssetResource>? filteredList, ChannelWriter<double> writer, CancellationToken cancellationToken)
        {
            var stats = new AssetStats();
            await MigrateInParallel(assets, filteredList, async (asset, cancellationToken) =>
            {
                var storage = _resourceProvider.GetBlobServiceClient(asset);

                var result = await MigrateAsync(account, storage, asset, cancellationToken);
                stats.Update(result);
                await writer.WriteAsync(stats.Total, cancellationToken);

            },
            _options.BatchSize,
            cancellationToken);

            writer.Complete();
            return stats;
        }

        private void WriteSummary(AssetStats stats)
        {
            var table = new Table()
                .AddColumn("Asset Type")
                .AddColumn("Count")
                .AddRow("Total", $"{stats.Total}")
                .AddRow("[green]Already Migrated[/]", $"[green]{stats.Migrated}[/]")
                .AddRow("[gray]Skipped[/]", $"[gray]{stats.Skipped}[/]")
                .AddRow("[green]Successful[/]", $"[green]{stats.Successful}[/]")
                .AddRow("[red]Failed[/]", $"[red]{stats.Failed}[/]");

            _console.Write(table);
        }

        public async Task<MigrationResult> MigrateAsync(
            MediaServicesAccountResource account,
            BlobServiceClient storage,
            MediaAssetResource asset,
            CancellationToken cancellationToken)
        {
            AssetMigrationResult result = new AssetMigrationResult(MigrationStatus.NotMigrated);
            _logger.LogInformation("Migrating asset: {name} (container {container}) ...", asset.Data.Name, asset.Data.Container);

            try
            {
                var container = storage.GetContainer(asset);

                if (!await container.ExistsAsync(cancellationToken))
                {
                    _logger.LogWarning("Container {name} missing for asset {asset}", container.Name, asset.Data.Name);
                    result.Status = MigrationStatus.Failed;
                    _logger.LogDebug("Migrated asset: {asset}, container: {container}, type: {type}, status: {status}", asset.Data.Name, asset.Data.Container, result.AssetType, result.Status);
                    return result;
                }

                // Get the initial migration status from the container level's metadata list.
                result = await _tracker.GetMigrationStatusAsync(container, cancellationToken);

                if (_options.SkipMigrated)
                {
                    if (result.Status == MigrationStatus.Completed)
                    {
                        _logger.LogDebug("Asset: {name} has already been migrated.", asset.Data.Name);

                        result.Status = MigrationStatus.AlreadyMigrated;
                        _logger.LogDebug("Migrated asset: {asset}, container: {container}, type: {type}, status: {status}", asset.Data.Name, asset.Data.Container, result.AssetType, result.Status);
                        return result;
                    }
                }

                var details = await asset.GetDetailsAsync(_logger, cancellationToken, _options.OutputManifest);
                var record = new AssetRecord(account, asset, details);

                // AssetType and ManifestName are not supposed to change for a specific input asset,
                // Set AssetType and manifest from the asset container before doing the actual transforming.
                if (details.Manifest != null)
                {
                    result.AssetType = details.Manifest.Format;
                    result.ManifestName = _options.OutputManifest ?? details.Manifest.FileName?.Replace(".ism", "");
                }
                else
                {
                    result.AssetType = AssetMigrationResult.AssetType_NonIsm;
                }

                if (result.IsSupportedAsset())
                {
                    var uploader = _transformFactory.GetUploader(_options);
                    var (Container, Path) = _transformFactory.TemplateMapper.ExpandAssetTemplate(
                                                        record.Asset,
                                                        _options.PathTemplate);

                    var canUpload = await uploader.CanUploadAsync(
                                                        Container,
                                                        Path,
                                                        cancellationToken);

                    if (canUpload)
                    {
                        try
                        {
                            foreach (var transform in _transformFactory.GetTransforms(_globalOptions, _options))
                            {
                                var transformResult = (AssetMigrationResult)await transform.RunAsync(record, cancellationToken);

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

                        _logger.LogWarning("Another tool is working on the container {container} and output path: {output}",
                                            Container,
                                            Path);
                    }
                }
                else
                {
                    // The asset type is not supported in this milestone,
                    // Mark the status as Skipped for caller to do the statistics.
                    result.Status = MigrationStatus.Skipped;

                    _logger.LogWarning("Skipping asset {name} because it is not in a supported format!!!", asset.Data.Name);
                }

                await _tracker.UpdateMigrationStatus(container, result, cancellationToken);

                _logger.LogDebug("Migrated asset: {asset}, container: {container}, type: {type}, status: {status}", asset.Data.Name, asset.Data.Container, result.AssetType, result.Status);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate asset {name}.", asset.Data.Name);
                result.Status = MigrationStatus.Failed;
                _logger.LogDebug("Migrated asset: {asset}, container: {container}, type: {type}, status: {status}", asset.Data.Name, asset.Data.Container, result.AssetType, result.Status);
                return result;
            }
        }
    }
}
