using AMSMigrate.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AMSMigrate.Ams
{
    class AssetMigrationResult : MigrationResult
    {
        public Uri? Uri { get; set; }

        public new MigrationStatus Status { get => base.Status; set => base.Status = value; }

        public AssetMigrationResult(MigrationStatus status = MigrationStatus.Skipped, Uri? uri = null) : base(status)
        {
            Uri = uri;
        }
    }

    internal class AssetMigrationTracker : IMigrationTracker<BlobContainerClient, AssetMigrationResult>
    {
        public const string StatusKey = "status";
        public const string UrlKey = "url";
        public const string MigratedBlobName = "__migrated";

        public async Task<AssetMigrationResult> GetMigrationStatusAsync(BlobContainerClient container, CancellationToken cancellationToken)
        {
            BlobContainerProperties properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (!properties.Metadata.TryGetValue(StatusKey, out var value) ||
                !Enum.TryParse<MigrationStatus>(value, out var status))
            {
                status = MigrationStatus.NotMigrated;
            }
            Uri? uri = null;
            if (properties.Metadata.TryGetValue(UrlKey, out var uriValue) && !string.IsNullOrEmpty(uriValue))
            {
                uri = new Uri(uriValue, UriKind.Absolute);
            }

            return new AssetMigrationResult(status, uri);
        }

        public async Task UpdateMigrationStatus(BlobContainerClient container, AssetMigrationResult result, CancellationToken cancellationToken)
        {
            var metadata = new Dictionary<string, string>
            {
                { "status", result.Status.ToString() },
                { "url", result.Uri?.ToString() ?? string.Empty }
            };
            await container.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        }
    }
}
