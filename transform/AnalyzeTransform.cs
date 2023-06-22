using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    class AnalysisResult : AssetMigrationResult
    {
        public AnalysisResult(string assetName, string? format, int locators, MigrationStatus status, Uri? uri = null)
            : base(status, uri)
        {
            AssetName = assetName;
            Format = format;
            Locators = locators;
        }

        public string? Format { get; }

        public int Locators { get; internal set; }

        public string AssetName { get; set; }
    }
    
    internal class AnalyzeTransform : ITransform<AssetDetails, AnalysisResult>
    {
        private readonly ILogger _logger;
        private readonly IMigrationTracker<BlobContainerClient, AssetMigrationResult> _tracker;

        public AnalyzeTransform(
            IMigrationTracker<BlobContainerClient, AssetMigrationResult> tracker,
            ILogger logger)
        {
            _tracker = tracker;
            _logger = logger;
        }

        public async Task<AnalysisResult> RunAsync(AssetDetails asset, CancellationToken cancellationToken)
        {
            var (assetName, container, manifest, _) = asset;
            // Check if already migrated.
            var status = await _tracker.GetMigrationStatusAsync(container, cancellationToken);
            return new AnalysisResult(assetName, manifest?.Format, 0, status.Status);
        }
    }
}
