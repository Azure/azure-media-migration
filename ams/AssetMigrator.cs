using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Media;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    record struct AssetStats(int Total, int Migrated, int Skipped, int Successful, int Failed, int Deleted);

    internal class AssetMigrator : BaseMigrator
    {
        private readonly ILogger _logger;
        private readonly TransformFactory _transformFactory;
        private readonly AssetOptions _options;

        public AssetMigrator(
            ILogger<AssetMigrator> logger,
            GlobalOptions globalOptions,
            AssetOptions assetOptions,
            TokenCredential credential,
            TransformFactory transformFactory):
            base(globalOptions, credential)
        {
            _logger = logger;
            _transformFactory = transformFactory;
            _options = assetOptions;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var account = await GetMediaAccountAsync(cancellationToken);
            _logger.LogInformation("Begin migration of assets for account: {name}", account.Data.Name);
            var totalAssets = await QueryMetricAsync(
                account.Id.ToString(),
                "AssetCount",
                cancellationToken: cancellationToken);

            var status = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Asset Migration", "Assets", totalAssets, status.Reader, cancellationToken);

            var stats = await MigrateAsync(account, status.Writer, cancellationToken);
            _logger.LogInformation("Finished migration of assets for account: {name}. Time taken: {time}", account.Data.Name, watch.Elapsed);
            await progress;
            WriteSummary(stats);
        }

        private async Task<AssetStats> MigrateAsync(MediaServicesAccountResource account, ChannelWriter<double> writer, CancellationToken cancellationToken)
        {
            var stats = new AssetStats();
            var orderBy = "properties/created";
            var assets = account.GetMediaAssets()
                .GetAllAsync(_globalOptions.ResourceFilter, orderby: orderBy, cancellationToken: cancellationToken);
            await MigrateInBatches(assets, async assets =>
            {
                var results = await Task.WhenAll(assets.Select(async asset => await MigrateAsync(account, asset, cancellationToken)));
                stats.Total += results.Length;
                foreach (var result in results)
                {
                    switch (result.Status)
                    {
                        case MigrationStatus.Success:
                            ++stats.Successful;
                            if (_options.DeleteMigrated)
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
                .AddRow("[gray]Skiped[/]", $"[gray]{stats.Skipped}[/]")
                .AddRow("[green]Successful[/]", $"[green]{stats.Successful}[/]")
                .AddRow("[red]Failed[/]", $"[red]{stats.Failed}[/]")
                .AddRow("[orange3]Deleted[/]", $"[orange3]{stats.Deleted}[/]");
            AnsiConsole.Write(table);
        }

        public async Task<MigrationResult> MigrateAsync(MediaServicesAccountResource account, MediaAssetResource asset, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Migrating asset: {name} ...", asset.Data.Name);
            var container = await asset.GetContainer(cancellationToken);
            if (await container.IsMigrated(cancellationToken))
                return MigrationStatus.AlreadyMigrated;

            try
            {
                var details = await asset.GetDetailsAsync(_logger, cancellationToken);
                var record = new AssetRecord(account, asset, details);
                foreach (var transform in _transformFactory.AssetTransforms)
                {
                    var result = await transform.RunAsync(record, cancellationToken);
                    if (result.Status != MigrationStatus.Skipped)
                    {
                        if (_options.DeleteMigrated && result.Status == MigrationStatus.Success)
                        {
                            _logger.LogWarning("Deleting asset {name} after migration", asset.Data.Name);
                            await asset.DeleteAsync(WaitUntil.Completed, cancellationToken);
                        }
                        return result;
                    }
                }

                _logger.LogWarning("Skipping asset {name} because it is not in a supported format!!!", asset.Data.Name);
                return MigrationStatus.Skipped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate asset {name}.", asset.Data.Name);
                return MigrationStatus.Failure;
            }
        }
    }
}
