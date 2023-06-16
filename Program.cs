using AMSMigrate.Ams;
using AMSMigrate.azure;
using AMSMigrate.Azure;
using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.Identity;
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

            var analyzeCommand = new Command("analyze", @"Analyze assets for migration and generate report.
Example(s):
amsmigrate analyze -s <subscriptionid> -g <resourcegroup> -n <account>
This will analyze the given media account and produce a summary report.");
            rootCommand.Add(analyzeCommand);
            analyzeCommand.SetHandler(async context => {
                var options = globalOptionsBinder.GetValue(context.BindingContext);
                await AnalyzeAssetsAsync(options, context.GetCancellationToken());
            });

            var description = @"Migrate Assets
Examples:
amsmigrate assets -s <subscription id> -g <resource group> -n <account name> -a <storage account> -t path-template
This migrates the assets to a different storage account in your subscription.";
            var assetOptionsBinder = new AssetOptionsBinder();
            var assetsCommand = assetOptionsBinder.GetCommand("assets", description);
            rootCommand.Add(assetsCommand);
            assetsCommand.SetHandler(
                async (globalOptions, assetOptions) => await MigrateAssetsAsync(globalOptions, assetOptions),
                globalOptionsBinder,
                assetOptionsBinder
                );

            var storageCommand = assetOptionsBinder.GetCommand("storage", @"Directly migrate the assets from the storage account.
Doesn't require the Azure media services to be running.");
            rootCommand.Add(storageCommand);
            storageCommand.SetHandler(async (globalOptions, assetOptions) =>
                await MigrateAssetsFromStorageAsync(globalOptions, assetOptions),
                globalOptionsBinder,
                assetOptionsBinder);

            var keyOptionsBinder = new KeyOptionsBinder();
            var keysCommand = keyOptionsBinder.GetCommand();
            rootCommand.Add(keysCommand);
            keysCommand.SetHandler(
                async (globalOptions, keyOptions) => await MigrateKeysAsync(globalOptions, keyOptions),
                globalOptionsBinder,
                keyOptionsBinder);

            var transformsCommand = new Command("transforms", "Migrate Transforms");
            rootCommand.Add(transformsCommand);
            transformsCommand.SetHandler(context =>
            {
                Console.Error.WriteLine("Transform migration not implemented yet!!");
                context.ExitCode = -1;
            });


            var eventsCommand = new Command("live", "Migrate Live Events");
            rootCommand.Add(eventsCommand);
            eventsCommand.SetHandler(context =>
            {
                Console.Error.WriteLine("Live Event migration not implemented yet!!");
                context.ExitCode = -1;
            });

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

        static IServiceCollection SetupServices(GlobalOptions options, TraceListener? listener = null)
        {
            var collection = new ServiceCollection()
                .AddSingleton<TokenCredential>(
                    new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        //ExcludeSharedTokenCacheCredential = true,
                        ExcludeInteractiveBrowserCredential = true
                    }))
                .AddSingleton(options)
                .AddSingleton<TemplateMapper>()
                .AddSingleton<TransMuxer>()
                .AddSingleton<TransformFactory>()
                .AddSingleton<AzureResourceProvider>()
                .AddSingleton<ICloudProvider, AzureProvider>()
                .AddSingleton<IFileUploader, AzureStorageUploader>()
                .AddSingleton<ISecretUploader, KeyVaultUploader>()
                .AddLogging(builder =>
                {
                    builder
                        .SetMinimumLevel(options.LogLevel)
                        .AddSpectreConsole(builder => builder.SetMinimumLevel(options.LogLevel));
                        //.AddSimpleConsole(options => { options.SingleLine = true; })
                        if (listener != null)
                        {
                            var logSwitch = new SourceSwitch("migration");
                            logSwitch.Level = SourceLevels.All;
                            builder.AddTraceSource(logSwitch, listener);
                        }
                });
            return collection;
        }

        static async Task AnalyzeAssetsAsync(GlobalOptions globalOptions, CancellationToken cancellationToken)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton<AssetAnalyzer>();
            var provider = collection.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Writing logs to {file}", globalOptions.LogFile); 
            await provider.GetRequiredService<AssetAnalyzer>().MigrateAsync(cancellationToken);
            logger.LogInformation("See file {file} for detailed logs.", globalOptions.LogFile);
        }

        static async Task MigrateAssetsAsync(GlobalOptions globalOptions, AssetOptions assetOptions)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton(assetOptions)
                .AddSingleton<PackagerFactory>()
                .AddSingleton<AssetMigrator>();
            var provider = collection.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Writing logs to {file}", globalOptions.LogFile);
            await provider.GetRequiredService<AssetMigrator>().MigrateAsync(default);
            logger.LogInformation("See file {file} for detailed logs.", globalOptions.LogFile);
        }

        static async Task MigrateAssetsFromStorageAsync(GlobalOptions globalOptions, AssetOptions assetOptions)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton(assetOptions)
                .AddSingleton<PackagerFactory>()
                .AddSingleton<StorageMigrator>();
            var provider = collection.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Writing logs to {file}", globalOptions.LogFile);
            await provider.GetRequiredService<StorageMigrator>().MigrateAsync(default);
            logger.LogInformation("See file {file} for detailed logs.", globalOptions.LogFile);
        }

        static async Task MigrateKeysAsync(GlobalOptions globalOptions, KeyOptions keyOptions)
        {
            using var listener = new TextWriterTraceListener(globalOptions.LogFile);
            var collection = SetupServices(globalOptions, listener)
                .AddSingleton<KeysMigrator>()
                .AddSingleton(keyOptions);
            var provider = collection.BuildServiceProvider();
            var migrator = provider.GetRequiredService<KeysMigrator>();
            await migrator.MigrateAsync(default);
        }
    }
}
