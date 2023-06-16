using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    public record AssetRecord(MediaServicesAccountResource Account, MediaAssetResource Asset, string AssetName, BlobContainerClient Container, Manifest? Manifest, ClientManifest? ClientManifest) 
        : AssetDetails(AssetName, Container, Manifest, ClientManifest)
    {
        public AssetRecord(MediaServicesAccountResource account, MediaAssetResource asset, AssetDetails details):
            this(account, asset, details.AssetName, details.Container, details.Manifest, details.ClientManifest)
        {
        }
    }

    class AssetTransform : ITransform<AssetRecord>
    {
        private readonly StorageTransform _transform;
        private readonly TemplateMapper _templateMapper;
        private readonly AssetOptions _options;
        protected readonly ILogger _logger;

        public AssetTransform(
            AssetOptions options,
            TemplateMapper templateMapper,
            StorageTransform transform,
            ILogger logger)
        {
            _logger = logger;
            _options = options;
            _templateMapper = templateMapper;
            _transform = transform;
        }

        public async Task<MigrationResult> RunAsync(AssetRecord record, CancellationToken cancellationToken)
        {
            var output = _templateMapper.ExpandAssetTemplate(record.Asset, _options.PathTemplate);
            return await _transform.RunAsync(record, output, cancellationToken);
        }
    }
}
