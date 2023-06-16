using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    public record AssetDetails(string AssetName, BlobContainerClient Container, Manifest? Manifest, ClientManifest? ClientManifest);

    internal abstract class StorageTransform : ITransform<AssetDetails>
    {
        protected readonly AssetOptions _options;
        private readonly TemplateMapper _templateMapper;
        protected readonly ILogger _logger;

        public StorageTransform(
            AssetOptions options,
            TemplateMapper templateMapper,
            ILogger logger)
        {
            _logger = logger;
            _options = options;
            _templateMapper = templateMapper;
        }

        public Task<MigrationResult> RunAsync(AssetDetails details, CancellationToken cancellationToken)
        {
            var outputPath = _templateMapper.ExpandPathTemplate(details.Container, _options.PathTemplate);
            return RunAsync(details, outputPath, cancellationToken); ;
        }

        public async Task<MigrationResult> RunAsync(
            AssetDetails details,
            (string Container, string Path) outputPath,
            CancellationToken cancellationToken)
        {
            _logger.LogTrace("Asset {asset} is in format: {format}.", details.AssetName, details.Manifest?.Format);
            if (details.Manifest != null && details.Manifest.IsLive)
            {
                _logger.LogWarning("Skipping asset {asset} which is from a running live event. Rerun the migration after the live event is stopped.", details.AssetName);
                return MigrationStatus.Skipped;
            }

            if (IsSupported(details))
            {
                var result = await TransformAsync(details, outputPath, cancellationToken);

                if (result)
                {
                    //upload a dummy blob to mark migration done.
                    if (_options.MarkCompleted)
                    {
                        await details.Container.MarkCompletedAsync(cancellationToken);
                    }
                    return MigrationStatus.Success;
                }
                else
                {
                    return MigrationStatus.Failure;
                }
            }

            return MigrationStatus.Skipped;
        }

        public abstract bool IsSupported(AssetDetails details);

        public abstract Task<bool> TransformAsync(
            AssetDetails details,
            (string Container, string Prefix) outputPath,
            CancellationToken cancellationToken = default);
    }
}
