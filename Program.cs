using AMSMigrate.Ams;
using AMSMigrate.Azure;
using AMSMigrate.Contracts;
using AMSMigrate.Local;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text;
using Vertical.SpectreLogger;

namespace AMSMigrate
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var globalOptionsBinder = new GlobalOptionsBinder();
            var rootCommand = globalOptionsBinder.GetCommand();

            var analysisOptionsBinder = new AnalysisOptionsBinder();
            var analyzeCommand = analysisOptionsBinder.GetCommand("analyze", @"Analyze assets for migration and generate report.
Example(s):
amsmigrate analyze -s <subscriptionid> -g <resourcegroup> -n <account>
This will analyze the given media account and produce a summary report.");
            rootCommand.Add(analyzeCommand);
            analyzeCommand.SetHandler(async context => {
                var globalOptions = globalOptionsBinder.GetValue(context.BindingContext);
                var analysisOptions = analysisOptionsBinder.GetValue(context.BindingContext);
                await AnalyzeAssetsAsync(globalOptions, analysisOptions, context.GetCancellationToken());
            });

            var description = @"Migrate Assets
Examples:
amsmigrate assets -s <subscription id> -g <resource group> -n <ams account name> -o <output storage account> -t path-template
This migrates the assets to a different storage account in your subscription.";
            var assetOptionsBinder = new AssetOptionsBinder();
            var assetsCommand = assetOptionsBinder.GetCommand("assets", description);
            rootCommand.Add(assetsCommand);
            assetsCommand.SetHandler(
                async context =>
                {
                    var globalOptions = globalOptionsBinder.GetValue(context.BindingContext);
                    var assetOptions = assetOptionsBinder.GetValue(context.BindingContext);
                    await MigrateAssetsAsync(globalOptions, assetOptions, context.GetCancellationToken());
                });

// disable storage migrate option until ready
/*
            var storageOptionsBinder = new StorageOptionsBinder();
            var storageCommand = storageOptionsBinder.GetCommand("storage", @"Directly migrate the assets from the storage account.
Doesn't require the Azure media services to be running.
Examples:
amsmigrate storage -s <subscription id> -g <resource group> -n <source storage account> -o <output storage account> -t path-template
");
            rootCommand.Add(storageCommand);
            storageCommand.SetHandler(async context =>
            {
                var globalOptions = globalOptionsBinder.GetValue(context.BindingContext);
                var storageOptions = storageOptionsBinder.GetValue(context.BindingContext);
                await MigrateStorageAsync(globalOptions, storageOptions, context.GetCancellationToken());
            });
*/

// disable key migrate option until ready
/*
            var keyOptionsBinder = new KeyOptionsBinder();
            var keysCommand = keyOptionsBinder.GetCommand();
            rootCommand.Add(keysCommand);
            keysCommand.SetHandler(
                async context =>
                {
                    var globalOptions = globalOptionsBinder.GetValue(context.BindingContext);
                    var keyOptions = keyOptionsBinder.GetValue(context.BindingContext);
                    await MigrateKeysAsync(globalOptions, keyOptions, context.GetCancellationToken());
                });
*/

            var parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseHelp(ctx =>
                {
                    ctx.HelpBuilder.CustomizeLayout(_ =>
                    {
                        return HelpBuilder.Default
                            .GetLayout()
                            .Skip(1)
                            .Prepend(_ =>
                                AnsiConsole.Write(
                                    new FigletText(rootCommand.Description!)
                                    .Color(Color.CadetBlue)
                                    .Centered())
                        );
                    });
                })
                .Build();
            return await parser.InvokeAsync(args);
        }

        static IServiceCollection SetupServices(GlobalOptions options, TraceListener listener)
        {
            var console = AnsiConsole.Console;

            var collection = new ServiceCollection()
                .AddSingleton<TokenCredential>(new DefaultAzureCredential(includeInteractiveCredentials: true))
                .AddSingleton(options)
                .AddSingleton(console)
                .AddSingleton<IMigrationTracker<BlobContainerClient, AssetMigrationResult>, AssetMigrationTracker>()
                .AddSingleton<TemplateMapper>()
                .AddSingleton<AzureResourceProvider>()
                .AddLogging(builder =>
                {
                    var logSwitch = new SourceSwitch("migration")
                    {
                        Level = SourceLevels.All
                    };
                    builder
                        .SetMinimumLevel(LogLevel.Trace)
                        .AddSpectreConsole(builder =>
                            builder
                                .SetMinimumLevel(options.LogLevel)
                                .UseConsole(console)
                                .WriteInBackground())
                        .AddTraceSource(logSwitch, listener);
                });
            if (options.CloudType == CloudType.Local)
            {
                collection.AddSingleton<ICloudProvider, LocalFileProvider>();
            }
            else
            {
                collection
                    .AddSingleton<ICloudProvider, AzureProvider>();
            }

            return collection;
        }

        static async Task AnalyzeAssetsAsync(
            GlobalOptions globalOptions,
            AnalysisOptions analysisOptions,
            CancellationToken cancellationToken)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton(analysisOptions)
                .AddSingleton<AssetAnalyzer>();
            var provider = collection.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Writing logs to {file}", globalOptions.LogFile);
            if (analysisOptions.AnalysisType == AnalysisType.Report)
            {
                logger.LogDebug("Writing html report to {file}", globalOptions.ReportFile);
            }
            await provider.GetRequiredService<AssetAnalyzer>().MigrateAsync(cancellationToken);
            logger.LogInformation("See file {file} for detailed logs.", globalOptions.LogFile);
            if (analysisOptions.AnalysisType == AnalysisType.Report)
            {
                logger.LogInformation("See file {file} for detailed html report.", globalOptions.ReportFile);
            }
        }

        static async Task MigrateAssetsAsync(
            GlobalOptions globalOptions,
            AssetOptions assetOptions,
            CancellationToken cancellationToken)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton(assetOptions)
                .AddSingleton<TransformFactory<AssetOptions>>()
                .AddSingleton<AssetMigrator>();
            var provider = collection.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Writing logs to {file}", globalOptions.LogFile);
            await provider.GetRequiredService<AssetMigrator>().MigrateAsync(cancellationToken);
            logger.LogInformation("See file {file} for detailed logs.", globalOptions.LogFile);
        }

        static async Task MigrateStorageAsync(
            GlobalOptions globalOptions,
            StorageOptions storageOptions,
            CancellationToken cancellationToken)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton(storageOptions)
                .AddSingleton<TransformFactory<StorageOptions>>()
                .AddSingleton<StorageMigrator>();
            var provider = collection.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Writing logs to {file}", globalOptions.LogFile);
            await provider.GetRequiredService<StorageMigrator>().MigrateAsync(cancellationToken);
            logger.LogInformation("See file {file} for detailed logs.", globalOptions.LogFile);
        }

        static async Task MigrateKeysAsync(
            GlobalOptions globalOptions,
            KeyOptions keyOptions,
            CancellationToken cancellationToken)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton<KeysMigrator>()
                .AddSingleton(keyOptions);
            var provider = collection.BuildServiceProvider();
            var migrator = provider.GetRequiredService<KeysMigrator>();
            await migrator.MigrateAsync(cancellationToken);
        }
    }
}
