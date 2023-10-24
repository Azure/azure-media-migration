using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class StorageOptionsBinder : BinderBase<StorageOptions>
    {
        private readonly Option<string> _sourceAccount = new Option<string>(
             aliases: new[] { "--source-account-name", "-n" },
             description: "The source storage account name.")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string> _storageAccount = new(
            aliases: new[] { "--output-storage-account", "-o" },
            description: @"The storage account to upload the migrated assets.
This is specific to the cloud you are migrating to.
For Azure specify the storage account name or the URL <https://accountname.blob.core.windows.net>")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string?> _pathTemplate = new(
            aliases: new[] { "--path-template", "-t" },
            () => "ams-migration-output/${ContainerName}/",
            description: @"Path template to determine the final path in the storage where files are uploaded.
Can use ${ContainerName}, 
e.g., ams-migration-output/${ContainerName} will upload to a container named 'ams-migration-output' with path beginning with the input container name.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<string?> _outputManifest = new Option<string?>(
            aliases: new[] { "--output-manifest-name", "-m" },
            description: @"The output manifest name without extension,
if it is not set, use input asset's manifest name.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<string?> _prefix = new Option<string?>(
            aliases: new[] { "--prefix", "-p" },
            description: @"")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        private readonly Option<Packager> _packagerType = new(
    aliases: new[] { "--packager" },
    () => Packager.Shaka,
    description: "The packager to use.")
        {
            IsHidden = true,
            IsRequired = false
        };

        private readonly Option<string?> _workingDirectory = new(
            aliases: new[] { "--working-dir" },
            () => Path.Combine(Path.GetTempPath(), "AMSMigrate"),
            description: @"The working directory to use for temporary files during packaging.")
        {
            IsRequired = false
        };

        private readonly Option<bool> _skipMigrated = new(
            aliases: new[] { "--skip-migrated" },
            () => true,
            description: @"Skip assets that have been migrated already.");

        private readonly Option<bool> _copyNonStreamable = new(
            aliases: new[] { "--copy-nonstreamable" },
            () => true,
            description: @"Copy non-streamable assets (Assets without .ism file) as is.");

        private readonly Option<bool> _overwrite = new(
            aliases: new[] { "-y", "--overwrite" },
            () => true,
            description: @"Overwrite the files in the destination.");

        private readonly Option<bool> _breakOutputLease = new(
            aliases: new[] { "--break-output-lease" },
            () => false,
            description: @"unconditionally break lease on output asset. [for debugging only]");

        private readonly Option<bool> _keepWorkingFolder = new(
            aliases: new[] { "--keep-working-folder" },
            () => false,
            description: @"Keep working folder. [for debugging only]");

        const int DefaultBatchSize = 1;
        private readonly Option<int> _batchSize = new(
            aliases: new[] { "--batch-size", "-b" },
            () => DefaultBatchSize,
            description: @"Batch size for parallel processing.");

        private readonly Option<bool> _encryptContent = new(
            aliases: new[] { "-e", "--encrypt-content" },
            () => false,
            description: "Encrypt the content  using CENC"
            );

        private readonly Option<Uri?> _keyVaultUri = new(
            aliases: new[] { "--key-vault-uri" },
            description: "The key vault to store encryption keys."
            );

        const int SegmentDurationInSeconds = 2;

        public StorageOptionsBinder()
        {
            _batchSize.AddValidator(result =>
            {
                var value = result.GetValueOrDefault<int>();
                if (value < 1 || value > 10)
                {
                    result.ErrorMessage = "Invalid batch size. Only values 1..10 are supported";
                }
            });

            _pathTemplate.AddValidator(result =>
            {
                var value = result.GetValueOrDefault<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    var (ok, key) = TemplateMapper.Validate(value, TemplateType.Assets);
                    if (!ok)
                    {
                        result.ErrorMessage = $"Invalid template: {value}. Template key '{key}' is invalid.";
                    }
                }
            });
        }

        public Command GetCommand(string name, string description)
        {
            var command = new Command(name, description);
            command.AddOption(_sourceAccount);
            command.AddOption(_storageAccount);
            command.AddOption(_pathTemplate);
            command.AddOption(_outputManifest);
            command.AddOption(_prefix);
            command.AddOption(_overwrite);
            command.AddOption(_skipMigrated);
            command.AddOption(_breakOutputLease);
            command.AddOption(_keepWorkingFolder);
            command.AddOption(_packagerType);
            command.AddOption(_workingDirectory);
            command.AddOption(_copyNonStreamable);
            command.AddOption(_batchSize);
            command.AddOption(_encryptContent);
            command.AddOption(_keyVaultUri);
            return command;
        }

        protected override StorageOptions GetBoundValue(BindingContext bindingContext)
        {
            var workingDirectory = bindingContext.ParseResult.GetValueForOption(_workingDirectory)!;
            Directory.CreateDirectory(workingDirectory);
            return new StorageOptions(
                bindingContext.ParseResult.GetValueForOption(_sourceAccount)!,
                bindingContext.ParseResult.GetValueForOption(_storageAccount)!,
                bindingContext.ParseResult.GetValueForOption(_packagerType),
                bindingContext.ParseResult.GetValueForOption(_pathTemplate)!,
                bindingContext.ParseResult.GetValueForOption(_outputManifest)!,
                bindingContext.ParseResult.GetValueForOption(_prefix),
                workingDirectory,
                bindingContext.ParseResult.GetValueForOption(_copyNonStreamable),
                bindingContext.ParseResult.GetValueForOption(_overwrite),
                bindingContext.ParseResult.GetValueForOption(_skipMigrated),
                bindingContext.ParseResult.GetValueForOption(_breakOutputLease),
                bindingContext.ParseResult.GetValueForOption(_keepWorkingFolder),
                SegmentDurationInSeconds,
                bindingContext.ParseResult.GetValueForOption(_batchSize),
                bindingContext.ParseResult.GetValueForOption(_encryptContent),
                bindingContext.ParseResult.GetValueForOption(_keyVaultUri)
            );
        }

        public StorageOptions GetValue(BindingContext bindingContext) => GetBoundValue(bindingContext);
    }
}
