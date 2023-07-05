using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace AMSMigrate.Transform
{
    internal class UploadTransform : StorageTransform
    {
        public UploadTransform(
            MigratorOptions options,
            IFileUploader uploader,
            ILogger<UploadTransform> logger,
            TemplateMapper templateMapper) :
            base(options, templateMapper, uploader, logger)
        {
        }

        // simple upload is supported except for live/live archive assets.
        protected override bool IsSupported(AssetDetails details)
            => details.Manifest == null || !(details.Manifest.IsLive || details.Manifest.IsLiveArchive);

        protected override async Task<string> TransformAsync(
            AssetDetails details,
            (string Container, string Prefix) outputPath,
            CancellationToken cancellationToken = default)
        {
            var (assetName, inputContainer, manifest, _) = details;
            var inputBlobs = await inputContainer.GetListOfBlobsAsync(cancellationToken, manifest);
            var uploads = inputBlobs.Select(blob => UploadBlobAsync(blob, outputPath, cancellationToken));
            await Task.WhenAll(uploads);

            // Mark the output container appropriately so that it won't be used as an input asset in new run.
            await UpdateOutputStatus(outputPath.Container, cancellationToken);

            return outputPath.Prefix;
        }
    }
}
