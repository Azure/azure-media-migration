using AMSMigrate.Ams;
using AMSMigrate.Azure;
using AMSMigrate.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    public record AssetDetails(string AssetName, BlobContainerClient Container, Manifest? Manifest, ClientManifest? ClientManifest, string? OutputManifest);

    internal abstract class StorageTransform : ITransform<AssetDetails, AssetMigrationResult>
    {
        protected readonly MigratorOptions _options;
        private readonly TemplateMapper _templateMapper;
        protected readonly ILogger _logger;
        protected readonly IFileUploader _fileUploader;     

        public StorageTransform(
            MigratorOptions options,
            TemplateMapper templateMapper,
            IFileUploader fileUploader,
            ILogger logger)
        {
            _options = options;
            _templateMapper = templateMapper;
            _fileUploader = fileUploader;
            _logger = logger;
        }

        public Task<AssetMigrationResult> RunAsync(AssetDetails details, CancellationToken cancellationToken)
        {
            var outputPath = _templateMapper.ExpandPathTemplate(details.Container, _options.PathTemplate);
            return RunAsync(details, outputPath, cancellationToken);
        }

        public async Task<AssetMigrationResult> RunAsync(
            AssetDetails details,
            (string Container, string Path) outputPath,
            CancellationToken cancellationToken)
        {
            var result = new AssetMigrationResult();

            _logger.LogTrace("Asset {asset} is in format: {format}.", details.AssetName, details.Manifest?.Format);
            if (details.Manifest != null && details.Manifest.IsLive)
            {
                _logger.LogWarning("Skipping asset {asset} which is from a running live event. Rerun the migration after the live event is stopped.", details.AssetName);
                result.Status = MigrationStatus.Skipped;
                return result;
            }

            if (IsSupported(details))
            {
                try
                {
                    var path = await TransformAsync(details, outputPath, cancellationToken);
                    result.Status = MigrationStatus.Completed;
                    result.OutputPath = _fileUploader.GetDestinationUri(outputPath.Container, path);
                }
                catch (Exception)
                {
                    result.Status = MigrationStatus.Failed;
                }
            }

            return result;
        }

        protected abstract bool IsSupported(AssetDetails details);

        protected abstract Task<string> TransformAsync(
            AssetDetails details,
            (string Container, string Prefix) outputPath,
            CancellationToken cancellationToken = default);

        protected async Task UploadBlobAsync(
            BlockBlobClient blob,
            (string Container, string Prefix) outputPath,
            CancellationToken cancellationToken)
        {
            var (container, prefix) = outputPath;
            var blobName = prefix == null ? blob.Name : $"{prefix}{blob.Name}";
            // hack optimization for direct blob copy.
            if (_fileUploader is AzureStorageUploader uploader)
            {
                await uploader.UploadBlobAsync(container, blobName, blob, cancellationToken);
            }
            else
            {
                var progress = new Progress<long>(progress =>
                 _logger.LogTrace("Upload progress for {name}: {progress}", blobName, progress));

                var result = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
                await _fileUploader.UploadAsync(container, blobName, result.Value.Content, progress, cancellationToken);
            }
        }

        protected async Task UpdateOutputStatus(
            string containerName,
            CancellationToken cancellationToken)
        {
            if (_fileUploader is AzureStorageUploader uploader)
            {
                await uploader.UpdateOutputStatus(containerName, cancellationToken);
            }
        }
    }
}
