using AMSMigrate.Ams;
using AMSMigrate.Contracts;

namespace AMSMigrate.Transform
{
    class AnalysisResult : AssetMigrationResult
    {
        public AnalysisResult(string assetName, MigrationStatus status, int locators, Uri? outputPath = null, string? assetType = null, string? manifestName = null)
            : base(status, outputPath, assetType, manifestName)
        {
            AssetName = assetName;
            Locators = locators;
        }

        public int Locators { get; internal set; }

        public string AssetName { get; set; }        
    }
    
    /* TODO:  This class is not required anymore.
     
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
            return new AnalysisResult(assetName, status.Status, 0, status.OutputPath, status.AssetType, status.ManifestName);
        }
    } */
}
