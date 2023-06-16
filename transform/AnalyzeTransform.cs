using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    class AnalysisResult : MigrationResult
    {
        public AnalysisResult(string? format, int locators, bool migrated) : base(MigrationStatus.Success)
        {
            Format = format;
            Locators = locators;
            Migrated = migrated;
        }

        public string? Format { get; }

        public int Locators { get; internal set; }

        public bool Migrated { get; }  
    }
    
    internal class AnalyzeTransform : ITransform<BlobContainerClient>
    {
        private readonly ILogger _logger;
        public AnalyzeTransform(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<MigrationResult> RunAsync(BlobContainerClient container, CancellationToken cancellationToken)
        {
            // Check if already migrated.
            var migrated = await container.IsMigrated(cancellationToken);
            Manifest? manifest = await container.LookupManifestAsync(container.Name, _logger, cancellationToken);
            return new AnalysisResult(manifest?.Format, 0, migrated);
        }
    }

    class AnalyzeAssetTransform : ITransform<MediaAssetResource>
    {
        private readonly ILogger _logger;
        private readonly AnalyzeTransform _transform;

        public AnalyzeAssetTransform(ILogger logger) 
        {
            _logger = logger;
            _transform = new AnalyzeTransform(logger);
        }

        public async Task<MigrationResult> RunAsync(MediaAssetResource asset, CancellationToken cancellationToken)
        {
            var container = await asset.GetContainer(cancellationToken);
            var result =  (AnalysisResult) await _transform.RunAsync(container, cancellationToken);
            var locators = asset.GetStreamingLocatorsAsync(cancellationToken).AsPages();
            var locatorCount = 0;
            await foreach (var page in locators)
            {
                locatorCount = page.Values.Count;
                break;
            }
            result.Locators = locatorCount;
            return result;
        }
    }
}
