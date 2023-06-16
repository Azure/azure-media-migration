using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.ResourceManager.Media;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    record struct Statistics(int TotalAssets, int StreamableAssets, int NoLocators);

    internal class AssetAnalyzer : BaseMigrator
    {
        private readonly ILogger _logger;
        private readonly AnalyzeAssetTransform _analyzer;

        public AssetAnalyzer(GlobalOptions options, TokenCredential credential, ILogger<AssetAnalyzer> logger)
            : base(options, credential)
        {
            _logger = logger;
            _analyzer = new AnalyzeAssetTransform(logger);
        }

        private async Task<AnalysisResult?> AnalyzeAsync(MediaAssetResource asset, CancellationToken cancellationToken)
        {
            _logger.LogDebug( "Analyzing asset: {asset}", asset.Data.Name);
            try
            {
                return (AnalysisResult)await _analyzer.RunAsync(asset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze asset {name}", asset.Data.Name);
                return null;
            }
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Begin analysis of assets for account: {name}", _globalOptions.AccountName);
            var account = await GetMediaAccountAsync(cancellationToken);
            var watch = Stopwatch.StartNew();
            double totalAssets = await QueryMetricAsync(account.Id.ToString(), "AssetCount", cancellationToken);

            var assets = account.GetMediaAssets().GetAllAsync(cancellationToken: cancellationToken);
            var statistics = new Statistics();
            var assetTypes = new SortedDictionary<string, int>();

            var channel = Channel.CreateBounded<double>(1);
            var progress = ShowProgressAsync("Analyzing Assets", "Assets", totalAssets, channel.Reader, cancellationToken);
            var writer = channel.Writer;
            await MigrateInBatches(assets, async assets =>
            {
                var tasks = assets.Select(asset => AnalyzeAsync(asset, cancellationToken));
                var results = await Task.WhenAll(tasks);
                statistics.TotalAssets += assets.Length;
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
            10,
            cancellationToken);

            _logger.LogDebug("Finished analysis of assets for account: {name}", _globalOptions.AccountName);
            writer.Complete();
            await progress;
            WriteSummary(statistics, assetTypes);
        }

        private static void WriteSummary(Statistics statistics, IDictionary<string, int> assetTypes)
        {
            var table = new Table()
                .Title("[yellow]Asset Summary[/]")
                .HideHeaders()
                .AddColumn(string.Empty)
                .AddColumn(string.Empty)
                .AddRow("[yellow]Total[/]", $"{statistics.TotalAssets}")
                .AddRow("[yellow]Streamable[/]", $"{statistics.StreamableAssets}")
                .AddRow("[yellow]No locators[/]", $"{statistics.NoLocators}");
            AnsiConsole.Write(table);

            var formats = new Table()
                .Title("[yellow]Asset Formats[/]")
                .HideHeaders()
                .AddColumn("Format")
                .AddColumn("Count");
            foreach (var (key, value) in assetTypes)
            {
                formats.AddRow($"[green]{key}[/]", $"[grey]{value}[/]");
            }
            AnsiConsole.Write(formats);
        }
    }
}
