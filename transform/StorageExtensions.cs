using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Transform
{
    static class StorageExtensions
    {
        public const string MigratedBlobName = "__migrated";
        public const int PAGE_SIZE = 1024;

        // AMS specific files that can be excluded since they are of no use outside AMS.
        public static readonly string[] ExcludedFiles =
        {
            ".ism",
            ".ismc",
            ".ismx",
            ".mpi"
        };

        public static async Task<Manifest?> LookupManifestAsync(
            this BlobContainerClient container,
            string name,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            BlobItem[]? manifests = null;
            var pages = container.GetBlobsByHierarchyAsync(prefix: string.Empty, delimiter: "/", cancellationToken: cancellationToken)
                .AsPages(pageSizeHint: PAGE_SIZE);
            await foreach (var page in pages)
            {
                manifests = page.Values
                    .Where(p => p.IsBlob && p.Blob.Name.EndsWith(".ism", StringComparison.InvariantCultureIgnoreCase))
                    .Select(p => p.Blob)
                    .ToArray();
                if (manifests.Any())
                    break;
            }

            Manifest? manifest = null;
            if (manifests == null || manifests.Length == 0)
            {
                logger.LogWarning("No manifest (.ism file) found in container {name}", name);
            }
            else
            {
                if (manifests.Length > 1)
                {
                    logger.LogWarning(
                    "Multiple manifests (.ism) present in container {name}. Only processing the first one {manifest}",
                    container.Name,
                    manifests[0].Name);
                }
                manifest = await GetManifestAsync(container, manifests[0].Name, logger, cancellationToken);
                logger.LogTrace("Found manifest {name} of format {format} in container {container}", manifest.FileName, manifest.Format, container.Name);
            }

            return manifest;
        }

        public static async Task<ClientManifest> GetClientManifestAsync(this BlobContainerClient container, Manifest manifest, ILogger logger, CancellationToken cancellationToken)
        {
            if (manifest.ClientManifest == null) throw new ArgumentException("No client manifest found", nameof(manifest));
            var blob = container.GetBlockBlobClient(manifest.ClientManifest);
            logger.LogDebug("Getting client manifest {manifest} for asset", manifest.ClientManifest);
            using BlobDownloadStreamingResult result =
                await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);

            return ClientManifest.Parse(result.Content, blob.Name, logger);
        }

        public static async Task<Manifest> GetManifestAsync(
            this BlobContainerClient container,
            string manifestName,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var blobClient = container.GetBlockBlobClient(manifestName);

            using BlobDownloadStreamingResult result =
                await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            var manifest = Manifest.Parse(result.Content, manifestName, logger);
            return manifest;
        }

        public static async Task<IEnumerable<BlockBlobClient>> GetListOfBlobsAsync(
            this BlobContainerClient container,
            CancellationToken cancellationToken,
            Manifest? manifest = null)
        {
            // If manifest is present use tracks from manifest.
            if (manifest != null)
            {
                return manifest.Body.Tracks
                    .Select(track => track.Source)
                    .Distinct() // select distinct names since single file can have multiple tracks and hence repeated in .ism
                    .Select(track => container.GetBlockBlobClient(track));
            }

            return await GetListOfBlobsAsync(container, cancellationToken);
        }

        public static async Task<IEnumerable<BlockBlobClient>> GetListOfBlobsAsync(
            this BlobContainerClient container,
            CancellationToken cancellationToken)
        {
            // list the blobs from the storage.
            var pages = container.GetBlobsByHierarchyAsync(delimiter: "/", cancellationToken: cancellationToken).AsPages();
            await foreach (var page in pages)
            {
                return page.Values
                    .Where(b => b.IsBlob && !ExcludedFiles.Contains(Path.GetExtension(b.Blob.Name)))
                    .Where(b => b.Blob.Name != MigratedBlobName)
                    .Select(b => container.GetBlockBlobClient(b.Blob.Name));
            }

            return Array.Empty<BlockBlobClient>();
        }

        /// <summary>
        /// List of remaining blobs not specified in the manifest.
        /// </summary>
        public static async Task<IEnumerable<BlockBlobClient>> GetListOfBlobsRemainingAsync(
            this BlobContainerClient container,
            Manifest manifest,
            CancellationToken cancellationToken)
        {
            var blobs = await GetListOfBlobsAsync(container, cancellationToken);
            return blobs.Where(blob => !manifest.Tracks.Any(t => t.Source == blob.Name));
        }

        public static async Task<AssetDetails> GetDetailsAsync(
            this MediaAssetResource asset,
            ILogger logger,
            CancellationToken cancellationToken,
            bool includeClientManifest = true)
        {
            var container = await asset.GetContainerAsync(cancellationToken);
            return await container.GetDetailsAsync(logger, cancellationToken, asset.Data.Name, includeClientManifest);
        }

        public static async Task<AssetDetails> GetDetailsAsync(
            this BlobContainerClient container,
            ILogger logger,
            CancellationToken cancellationToken,
            string? name = null,
            bool includeClientManifest = true)
        {
            name = name ?? container.Name;
            var manifest = await container.LookupManifestAsync(name, logger, cancellationToken);
            ClientManifest? clientManifest = null;
            if (manifest != null && includeClientManifest && manifest.IsLiveArchive)
            {
                clientManifest = await container.GetClientManifestAsync(manifest, logger, cancellationToken);
            }
            return new AssetDetails(name, container, manifest, clientManifest);
        }
    }
}
