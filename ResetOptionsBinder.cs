using AMSMigrate.Contracts;
using FFMpegCore.Enums;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private readonly Option<bool> _all = new Option<bool>(
           aliases: new[] { "--all", "-a" },
           description: "Reset all assets in the account, regardless of their migration status.")
        {
            IsRequired = false
        };
        private readonly Option<bool> _failed = new Option<bool>(
          aliases: new[] { "--failed", "-f" },
          description: "Reset the failed migrated assets in the account, This is the default setting.")
        {
            IsRequired = false
        };
        public ResetOptions GetValue(BindingContext context) => GetBoundValue(context);

        public Command GetCommand(string name, string description)
        {
            var command = new Command(name, description);
            command.AddOption(_sourceAccount);
            command.AddOption(_all);
            command.AddOption(_failed);
            return command;
        }

        protected override ResetOptions GetBoundValue(BindingContext bindingContext)
        {
            return new ResetOptions(
                bindingContext.ParseResult.GetValueForOption(_sourceAccount)!,
                bindingContext.ParseResult.GetValueForOption(_all),
                bindingContext.ParseResult.GetValueForOption(_failed)
            );
        }
    }
}
