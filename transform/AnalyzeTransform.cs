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

        public Uri? OutputHlsUrl => OutputPath != null ? new Uri(OutputPath!, (ManifestName + ".m3u8")) : null;

        public Uri? OutputDashUrl => OutputPath != null ? new Uri(OutputPath!, (ManifestName + ".mpd")) : null;
    }
}
