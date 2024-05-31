using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class GlobalOptionsBinder : BinderBase<GlobalOptions>
    {

        private static readonly Option<LogLevel> _logLevel = new Option<LogLevel>(
            aliases: new[] { "--verbosity", "-v" },
#if DEBUG
            getDefaultValue: () => LogLevel.Debug,
#else
            getDefaultValue: () => LogLevel.Warning,
#endif
            description: "The log level for logging"
        );

        private static readonly Option<string?> _logDirectory = new Option<string?>(
            aliases: new[] { "-l", "--log-directory" },
            getDefaultValue: () => Environment.CurrentDirectory,
            description: @"The directory where the logs are written. Defaults to the working directory"
            );

        private static readonly Option<string> _tenant = new Option<string>(
            aliases: new[] { "--tenant" },
            description: "The azure tenant to use")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        private static readonly Option<string> _subscription = new Option<string>(
            aliases: new[] { "--subscription", "-s" },
            description: "The azure subscription to use")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private static readonly Option<string> _resourceGroup = new Option<string>(
            aliases: new[] { "--resource-group", "-g" },
            description: "The resource group containing the AMS account")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        public GlobalOptionsBinder()
        {
        }

        protected override GlobalOptions GetBoundValue(BindingContext bindingContext) => GetValue(bindingContext);

        public Command GetCommand()
        {
            var command = new RootCommand("Azure Media Services migration tool");
            command.AddGlobalOption(_logLevel);
            command.AddGlobalOption(_logDirectory);
            command.AddGlobalOption(_tenant);
            command.AddGlobalOption(_subscription);
            command.AddGlobalOption(_resourceGroup);

            return command;
        }

        public static GlobalOptions GetValue(BindingContext bindingContext)
        {
            return new GlobalOptions(
                bindingContext.ParseResult.GetValueForOption(_tenant)!,
                bindingContext.ParseResult.GetValueForOption(_subscription)!,
                bindingContext.ParseResult.GetValueForOption(_resourceGroup)!,
                CloudType.Azure, //TODO: add an option.
                bindingContext.ParseResult.GetValueForOption(_logLevel),
                bindingContext.ParseResult.GetValueForOption(_logDirectory)!
            );
        }
    }
}

