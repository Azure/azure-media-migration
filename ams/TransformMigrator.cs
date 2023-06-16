using AMSMigrate.Contracts;
using AMSMigrate.Transform;
using Azure.Core;
using Azure.ResourceManager.Media.Models;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Ams
{
    /// <summary>
    /// A dummy account level migrator. Does nothing at this point.
    /// </summary>
    internal class TransformMigrator : BaseMigrator
    {
        private readonly ILogger _logger;
        private readonly TransformFactory _transformFactory;

        public TransformMigrator(
            GlobalOptions options,
            TransformFactory transformFactory,
            ILogger<TransformMigrator> logger,
            TokenCredential credential) : 
            base(options, credential)
        {
            _transformFactory = transformFactory;
            _logger = logger;
        }

        public override async Task MigrateAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Begin migration for transforms account: {name}", _globalOptions.AccountName);
            var account = await GetMediaAccountAsync(cancellationToken);
            var migrator = _transformFactory.TransformTransform;
            var transforms = account.GetMediaTransforms();
            await foreach (var transform in account.GetMediaTransforms())
            {
                if (transform.Data.Outputs.All(o => o.Preset is StandardEncoderPreset || o.Preset is BuiltInStandardEncoderPreset))
                {
                    // Only encoding presets can be migrated.
                    var result = await migrator.RunAsync(transform, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Cannot migrate transform which is not an encoding transform. {name}", transform.Data.Name);
                }
            }
            _logger.LogInformation("Finshed migration of transforms for account {name}", _globalOptions.AccountName);
        }
    }
}
