using AMSMigrate.Ams;
using AMSMigrate.Contracts;

namespace AMSMigrate.Transform
{
    class AnalysisResult : AssetMigrationResult
    {
        public AnalysisResult(string assetName, MigrationStatus status, Uri? outputPath = null, string? assetType = null, string? manifestName = null)
            : base(status, outputPath, assetType, manifestName)
        {
            AssetName = assetName;

            LocatorIds = new List<string>();
        }

        // A list of Locator Guids of the asset,
        // it can have zero or multiple GUIDs.
        public List<string> LocatorIds { get; }

        public string AssetName { get; set; }

        public Uri? OutputHlsUrl => OutputPath != null ? new Uri(OutputPath!, (ManifestName + ".m3u8")) : null;

        public Uri? OutputDashUrl => OutputPath != null ? new Uri(OutputPath!, (ManifestName + ".mpd")) : null;
    }
}
