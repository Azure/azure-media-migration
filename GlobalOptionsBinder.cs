using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class GlobalOptionsBinder : BinderBase<GlobalOptions>
    {

        private readonly Option<LogLevel> _logLevel = new Option<LogLevel>(
            aliases: new[] { "--verbosity", "-v" },
#if DEBUG
            getDefaultValue: () => LogLevel.Debug,
#else
            getDefaultValue: () => LogLevel.Warning,
#endif
            description: "The log level for logging"
        );

        private readonly Option<string?> _logDirectory = new Option<string?>(
            aliases: new[] { "-l", "--log-directory" },
            getDefaultValue: () => Environment.CurrentDirectory,
            description: @"The directory where the logs are written. Defaults to the working directory"
            );

        private readonly Option<string> _subscription = new Option<string>(
            aliases: new[] { "--subscription", "-s" },
            description: "The azure subscription to use")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string> _resourceGroup = new Option<string>(
            aliases: new[] { "--resource-group", "-g" },
            description: "The resource group containing the AMS account")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string> _mediaAccount = new Option<string>(
            aliases: new[] { "--account-name", "-n" },
            description: "Azure Media Services Account name")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string?> _filter = new Option<string?>(
            aliases: new[] { "--resource-filter", "-f" },
            description: @"An ODATA condition to filter the resources.
e.g.: ""name eq 'asset1'"" to match an asset with name 'asset1'.
Visit https://learn.microsoft.com/en-us/azure/media-services/latest/filter-order-page-entities-how-to for more information.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        public GlobalOptionsBinder()
        {
        }

        protected override GlobalOptions GetBoundValue(BindingContext bindingContext)
        {
            var logFile = $"MigrationLog_{DateTime.Now:HH_mm_ss}.txt";

            return new GlobalOptions(
                bindingContext.ParseResult.GetValueForOption(_subscription)!,
                bindingContext.ParseResult.GetValueForOption(_resourceGroup)!,
                bindingContext.ParseResult.GetValueForOption(_mediaAccount)!,
                bindingContext.ParseResult.GetValueForOption(_filter),
                bindingContext.ParseResult.GetValueForOption(_logLevel),
                logFile
            );
        }

        public Command GetCommand()
        {
            var command = new RootCommand("Azure Media Services migration tool");
            command.AddGlobalOption(_logLevel);
            command.AddGlobalOption(_subscription);
            command.AddGlobalOption(_resourceGroup);
            command.AddGlobalOption(_mediaAccount);
            command.AddGlobalOption(_filter);
            return command;
        }

        public GlobalOptions GetValue(BindingContext context) => GetBoundValue(context);
    }
}

