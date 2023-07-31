using AMSMigrate.Contracts;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class CleanupOptionsBinder : BinderBase<CleanupOptions>
    {
        private readonly Option<string> _sourceAccount = new Option<string>(
             aliases: new[] { "--source-account-name", "-n" },
             description: "Azure Media Services Account.")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string?> _filter = new Option<string?>(
            aliases: new[] { "--resource-filter", "-f" },
            description: @"An ODATA condition to filter the resources only when the source account is for media service.
e.g.: ""name eq 'asset1'"" to match an asset with name 'asset1'.
Visit https://learn.microsoft.com/en-us/azure/media-services/latest/filter-order-page-entities-how-to for more information.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<CleanupType> _cleanupType = new(
            aliases: new[] {"--cleanup-type", "-t" },
            () => CleanupType.AMSAccount,
            description: @"Cleanup StorageAccount or AMSAccount.
 -clean up StorageAccount,
 -cleanup AMSAccount")
        {
            IsRequired = false
        };

        private readonly Option<bool> _isForceCleanUpAsset = new(
          aliases: new[] {"--force-cleanup", "-x"},
          () => false,
          description: @"Force the cleanup of migrated assets if the setting is enabled")
        {
            IsRequired = false
        };

        private readonly Option<bool> _isCleanUpAccount = new(
        aliases: new[] {"--cleanup-account", "-ax"},
        () => false,
        description: @"Delete the whole ams account if the setting to enabled")
        {
            IsRequired = false
        };

        public CleanupOptions GetValue(BindingContext context) => GetBoundValue(context);

        public Command GetCommand(string name, string description)
        {
            var command = new Command(name, description);
            command.AddOption(_sourceAccount);
            command.AddOption(_filter);
            command.AddOption(_cleanupType);
            command.AddOption(_isForceCleanUpAsset);
            command.AddOption(_isCleanUpAccount);
            return command;
        }

        protected override CleanupOptions GetBoundValue(BindingContext bindingContext)
        {
            return new CleanupOptions(
                bindingContext.ParseResult.GetValueForOption(_sourceAccount),
                bindingContext.ParseResult.GetValueForOption(_cleanupType),
                bindingContext.ParseResult.GetValueForOption(_filter),
                bindingContext.ParseResult.GetValueForOption(_isForceCleanUpAsset),
                bindingContext.ParseResult.GetValueForOption(_isCleanUpAccount)
            );
        }
    }
}
