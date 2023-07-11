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
using System.CommandLine.Invocation;
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
                var analysisOptions = analysisOptionsBinder.GetValue(context.BindingContext);
                await AnalyzeAssetsAsync(context, analysisOptions, context.GetCancellationToken());
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
                    var assetOptions = assetOptionsBinder.GetValue(context.BindingContext);
                    await MigrateAssetsAsync(context, assetOptions, context.GetCancellationToken());
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
                .AddMiddleware(SetupDependencies)
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

        static async Task SetupDependencies(InvocationContext context, Func<InvocationContext, Task> next)
        {
            var globalOptions = GlobalOptionsBinder.GetValue(context.BindingContext);
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = new ServiceCollection();
            SetupServices(collection, globalOptions, listener);
            var provider = collection.BuildServiceProvider();
            context.BindingContext.AddService<IServiceProvider>(_ => provider);
            var logger = provider.GetRequiredService<ILogger<Program>>();

            logger.LogDebug("Writing logs to {file}", globalOptions.LogFile);
            await next(context);
            logger.LogInformation("See file {file} for detailed logs.", globalOptions.LogFile);
        }

        static void SetupServices(IServiceCollection collection, GlobalOptions options, TraceListener listener)
        {
            var console = AnsiConsole.Console;

            collection
                .AddSingleton<TokenCredential>(new DefaultAzureCredential(includeInteractiveCredentials: true))
                .AddSingleton(options)
                .AddSingleton(console)
                .AddSingleton<IMigrationTracker<BlobContainerClient, AssetMigrationResult>, AssetMigrationTracker>()
                .AddSingleton<TemplateMapper>()
                .AddSingleton<AzureResourceProvider>()
                .AddSingleton<TransformFactory>()
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
        }

        static async Task AnalyzeAssetsAsync(
            InvocationContext context,
            AnalysisOptions analysisOptions,
            CancellationToken cancellationToken)
        {
            var provider = context.BindingContext.GetRequiredService<IServiceProvider>();
            await ActivatorUtilities.CreateInstance<AssetAnalyzer>(provider, analysisOptions)
                .MigrateAsync(cancellationToken);
        }

        static async Task MigrateAssetsAsync(
            InvocationContext context,
            AssetOptions assetOptions,
            CancellationToken cancellationToken)
        {
            var provider = context.BindingContext.GetRequiredService<IServiceProvider>();
            await ActivatorUtilities.CreateInstance<AssetMigrator>(provider, assetOptions)
                .MigrateAsync(cancellationToken);
        }

        static async Task MigrateStorageAsync(
            InvocationContext context,
            StorageOptions storageOptions,
            CancellationToken cancellationToken)
        {
            var provider = context.BindingContext.GetRequiredService<IServiceProvider>();
            await ActivatorUtilities.CreateInstance<StorageMigrator>(provider, storageOptions)
                .MigrateAsync(cancellationToken);
        }

        static async Task MigrateKeysAsync(
            InvocationContext context,
            KeyOptions keyOptions,
            CancellationToken cancellationToken)
        {
            var provider = context.BindingContext.GetRequiredService<IServiceProvider>();
            await ActivatorUtilities.CreateInstance<KeysMigrator>(provider, keyOptions)
                .MigrateAsync(cancellationToken);
        }
    }
}
