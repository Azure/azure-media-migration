using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMSMigrate
{
    internal class ResetOptionsBinder
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

    }
}
