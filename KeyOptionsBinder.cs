using AMSMigrate.Ams;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class KeyOptionsBinder : BinderBase<KeyOptions>
    {
        private readonly Option<Uri> _keyVaultUri = new Option<Uri>(
            aliases: new[] { "--vault-url", "-v" },
            description: @"The vault for migrating keys.
Specific to the cloud you are migrating to.
For Azure it is <https://valutname.azure.net>")
                {
                    IsRequired = true,
                    Arity = ArgumentArity.ExactlyOne
                };

        private readonly Option<string?> _keyTemplate = new Option<string?>(
            aliases: new[] { "--vault-template", "-t" },
            () => "${KeyId}",
            description: @"Template for the name in the vault with which the key is stored.
Can use ${KeyId} ${KeyName} in the template.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<int> _batchSize = new(
            aliases: new[] { "--batch-size", "-b" },
            () => 1,
            description: @"Batch size for processing.");

        public KeyOptionsBinder()
        {
            _keyTemplate.AddValidator(result => {
                var value = result.GetValueOrDefault<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    var (ok, key) = TemplateMapper.Validate(value, TemplateType.Keys);
                    if (!ok)
                    {
                        result.ErrorMessage = $"Invalid template: {value}. Template key '{key}' is invalid.";
                    }
                }
            });
        }

        public Command GetCommand()
        {
            var keysCommand = new Command("keys", "Migrate content keys");
            keysCommand.AddOption(_keyVaultUri);
            keysCommand.AddOption(_keyTemplate);
            keysCommand.AddOption(_batchSize);
            return keysCommand;
        }

        protected override KeyOptions GetBoundValue(BindingContext bindingContext)
        {
            return new KeyOptions(
                bindingContext.ParseResult.GetValueForOption(_keyVaultUri)!,
                bindingContext.ParseResult.GetValueForOption(_keyTemplate),
                bindingContext.ParseResult.GetValueForOption(_batchSize)
            );
        }
    }


}
