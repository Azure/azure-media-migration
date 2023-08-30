using AMSMigrate.Contracts;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class ResetOptionsBinder : BinderBase<ResetOptions>
    {
        private readonly Option<string> _sourceAccount = new Option<string>(
           aliases: new[] { "--source-account-name", "-n" },
           description: "Azure Media Services Account.")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string> _category = new Option<string>(
           aliases: new[] { "--category", "-c" },
           () => "all",
           description: "Define two categories: \"all\" and \"failed\". The \"all\" category encompasses a complete reset of all assets within the account, regardless of their migration status. By default, this parameter is set to \"all\". The \"failed\" category exclusively pertains to resetting only those assets that have encountered migration failures, reverting them back to their non-migrated state.")
        {
            IsRequired = false
        };
        public ResetOptions GetValue(BindingContext context) => GetBoundValue(context);

        public Command GetCommand(string name, string description)
        {
            var command = new Command(name, description);
            command.AddOption(_sourceAccount);
            command.AddOption(_category);
            return command;
        }

        protected override ResetOptions GetBoundValue(BindingContext bindingContext)
        {
            return new ResetOptions(
                bindingContext.ParseResult.GetValueForOption(_sourceAccount)!,
                bindingContext.ParseResult.GetValueForOption(_category)!
            );
        }
    }
}
