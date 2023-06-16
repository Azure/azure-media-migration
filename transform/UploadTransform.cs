using AMSMigrate.Ams;
using AMSMigrate.Azure;
using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    internal class UploadTransform : StorageTransform
    {
        private readonly IFileUploader _fileUploader;

        public UploadTransform(
            AssetOptions options,
            IFileUploader uploader,
            ILogger<UploadTransform> logger,
            TemplateMapper templateMapper) :
            base(options, templateMapper, logger)
        {
            _fileUploader = uploader;
        }

        // simple upload is supported except for live/live archive assets.
        public override bool IsSupported(AssetDetails details)
            => details.Manifest == null || !(details.Manifest.IsLive || details.Manifest.IsLiveArchive);

        public override async Task<bool> TransformAsync(
            AssetDetails details,
            (string Container, string Prefix) outputPath,
            CancellationToken cancellationToken = default)
        {
            var (assetName, inputContainer, manifest, _) = details;
            var inputBlobs = await inputContainer.GetListOfBlobsAsync(cancellationToken, manifest);

            var (containerName, outputPrefix) = outputPath;
            var uploads = inputBlobs.Select(async inputBlob =>
            {
                var blobName = outputPrefix == null ? inputBlob.Name : $"{outputPrefix}/{inputBlob.Name}";
                // hack optimization for direct blob copy.
                if (_fileUploader is AzureStorageUploader uploader)
                {
                    await uploader.UploadBlobAsync(containerName, blobName, inputBlob, cancellationToken);
                }
                else
                {
                    var progress = new Progress<long>(progress =>
                     _logger.LogTrace("Upload progress for {name}: {progress}", blobName, progress));

                    var result = await inputBlob.DownloadStreamingAsync(cancellationToken: cancellationToken);
                    await _fileUploader.UploadAsync(containerName, blobName, result.Value.Content, progress, cancellationToken);
                }
            });
            await Task.WhenAll(uploads);
            return true;
        }
    }
}
