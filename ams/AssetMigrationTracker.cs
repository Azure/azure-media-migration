using AMSMigrate.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AMSMigrate.Ams
{
    class AssetMigrationResult : MigrationResult
    {
        public const string AssetType_NonIsm = "non_ism";
        public const string AssetType_Encrypted = "encrypted";
        public const string AssetType_DmtGenerated = "dmt_generated";


        /// <summary>
        /// The asset type for the input asset, it is the value of "format" meta in .ism file if it exists,
        /// or other dedicated value when .ism file doesn't exist, such as "non_ism", "encrypted", "dmt_migrated" etc.
        /// </summary>
        public string? AssetType { get; set; }

        /// <summary>
        /// The manifest name without the extension .ism,  it is set only when the input asset contains .ism file.
        /// The ManifestName can be used to generate the final streaming URL for generated asset.
        /// </summary>
        public string? ManifestName { get; set; }

        /// <summary>
        /// The base URL for the generated asset, it could use a single storage container, or a sub-folder of the shared storage container.
        /// The URL is in format of https://{storage_account}.blob.core.windows.net/{containerName}/{sub_folder}/
        /// If the generated asset uses its own container, the {sub_folder} will be empty.
        /// </summary>
        public Uri? OutputPath { get; set; }

        public new MigrationStatus Status { get => base.Status; set => base.Status = value; }

        /// <summary>
        /// Determine if the input asset is streamable.
        /// </summary>
        public bool IsStreamable => (Status != MigrationStatus.Failed && AssetType != null && AssetType != AssetType_NonIsm);

        /// <summary>
        /// Determine if the asset can be migrated.
        /// The current supported scenarios:
        /// 
        ///   If it has .ism file, only mp4 format is supported.
        ///   If there is no .ism file,  it can be copied over directly.
        ///   
        /// </summary>
        public bool IsSupportedAsset(bool enableLiveAsset)
        {
            return (AssetType != null && (AssetType == AssetType_NonIsm
                || AssetType == "fmp4"
                || AssetType.StartsWith("mp4")
                || (enableLiveAsset && AssetType == "vod-fmp4")));
        }

        public AssetMigrationResult(MigrationStatus status = MigrationStatus.NotMigrated, Uri? outputPath = null, string? assetType = null, string? manifestName = null) : base(status)
        {
            AssetType = assetType;
            ManifestName = manifestName;
            OutputPath = outputPath;
        }
    }

    internal class AssetMigrationTracker : IMigrationTracker<BlobContainerClient, AssetMigrationResult>
    {
        internal const string AssetTypeKey = "AssetType";
        internal const string MigrateResultKey = "MigrateResult";
        internal const string ManifestNameKey = "ManifestName";
        internal const string OutputPathKey = "OutputPath";

        public async Task<AssetMigrationResult> GetMigrationStatusAsync(BlobContainerClient container, CancellationToken cancellationToken)
        {
            BlobContainerProperties properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken);
            var metadataList = properties.Metadata;

            Uri? outputPath = null;
            string? assetType = null;
            string? manifestName = null;
            MigrationStatus status = MigrationStatus.NotMigrated;

            if (metadataList != null && metadataList.Count > 0)
            {
                if (metadataList.TryGetValue(MigrateResultKey, out var value) && !string.IsNullOrEmpty(value))
                {
                    status = (MigrationStatus)Enum.Parse(typeof(MigrationStatus), value);
                }

                metadataList.TryGetValue(AssetTypeKey, out assetType);

                metadataList.TryGetValue(ManifestNameKey, out manifestName);

                if (metadataList.TryGetValue(OutputPathKey, out value) && !string.IsNullOrEmpty(value))
                {
                    outputPath = new Uri(value, UriKind.Absolute);
                }
            }

            return new AssetMigrationResult(status, outputPath, assetType, manifestName);
        }

        public async Task UpdateMigrationStatus(BlobContainerClient container, AssetMigrationResult result, CancellationToken cancellationToken)
        {
            var metadata = new Dictionary<string, string>();

            switch (result.Status)
            {
                // Report the same value in metadata for Completed and AlreadyMigrated.
                case MigrationStatus.Completed:
                case MigrationStatus.AlreadyMigrated:

                    metadata.Add(MigrateResultKey, "Completed");
                    break;

                case MigrationStatus.Failed:
                    metadata.Add(MigrateResultKey, "Failed");
                    break;
                default:
                    // Don't put value for all other status in the metadata list.
                    break;
            }

            if (!string.IsNullOrEmpty(result.AssetType))
            {
                metadata.Add(AssetTypeKey, result.AssetType);
            }

            if (!string.IsNullOrEmpty(result.ManifestName))
            {
                metadata.Add(ManifestNameKey, result.ManifestName);
            }

            if (result.OutputPath != null)
            {
                metadata.Add(OutputPathKey, result.OutputPath.AbsoluteUri);
            }

            await container.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        }
    }
}
