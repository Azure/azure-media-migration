using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using AMSMigrate.Decryption;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
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
            StorageEncryptedAssetDecryptionInfo? decryptInfo,
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
                logger.LogWarning("No manifest (.ism file) found in container {name}", container.Name);
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
                manifest = await GetManifestAsync(container, manifests[0].Name, logger, decryptInfo, cancellationToken);
                logger.LogTrace("Found manifest {name} of format {format} in container {container}", manifest.FileName, manifest.Format, container.Name);
            }

            return manifest;
        }

        public static async Task<ClientManifest> GetClientManifestAsync(this BlobContainerClient container, Manifest manifest, ILogger logger, StorageEncryptedAssetDecryptionInfo? decryptInfo, CancellationToken cancellationToken)
        {
            if (manifest.ClientManifest == null) throw new ArgumentException("No client manifest found", nameof(manifest));
            var blob = container.GetBlockBlobClient(manifest.ClientManifest);
            logger.LogDebug("Getting client manifest {manifest} for asset", manifest.ClientManifest);

            Stream ismcStream;

            using AesCtrTransform? aesTransform = AssetDecryptor.GetAesCtrTransform(decryptInfo, manifest.ClientManifest, false);

            if (aesTransform != null)
            {
                ismcStream = new MemoryStream();

                await AssetDecryptor.DecryptTo(aesTransform, blob, ismcStream, cancellationToken);

                ismcStream.Position = 0;
            }
            else
            {
                using BlobDownloadStreamingResult result =
                        await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);

                ismcStream = result.Content;
            }            

            return ClientManifest.Parse(ismcStream, blob.Name, logger);
        }

        public static async Task<Manifest> GetManifestAsync(
            this BlobContainerClient container,
            string manifestName,
            ILogger logger,
            StorageEncryptedAssetDecryptionInfo? decryptionInfo,
            CancellationToken cancellationToken)
        {
            var blobClient = container.GetBlockBlobClient(manifestName);

            Stream ismStream;

            using AesCtrTransform? aesTransform = AssetDecryptor.GetAesCtrTransform(decryptionInfo, manifestName, false);

            if (aesTransform != null)
            {
                ismStream = new MemoryStream();

                await AssetDecryptor.DecryptTo(aesTransform, blobClient, ismStream, cancellationToken);

                ismStream.Position = 0;
            }
            else
            {
                using BlobDownloadStreamingResult result =
                    await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

                ismStream = result.Content;
            }            

            return Manifest.Parse(ismStream, manifestName, logger); ;
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
            string? outputManifestname,
            bool includeClientManifest = true)
        {
            var container = await asset.GetContainerAsync(cancellationToken);
            StorageEncryptedAssetDecryptionInfo? decryptInfo = null;

            if (asset.Data.StorageEncryptionFormat != MediaAssetStorageEncryptionFormat.None)
            {
                // The asset is encypted with its own key and list of IVs.
                decryptInfo = await asset.GetEncryptionKeyAsync(cancellationToken);
            }

            return await container.GetDetailsAsync(logger, cancellationToken, outputManifestname, asset.Data.Name, includeClientManifest, decryptInfo);
        }

        public static async Task<AssetDetails> GetDetailsAsync(
            this BlobContainerClient container,
            ILogger logger,
            CancellationToken cancellationToken,
            string? outputManifestname,
            string? name = null,
            bool includeClientManifest = true,
            StorageEncryptedAssetDecryptionInfo? decryptInfo = null)
        {
            name = name ?? container.Name;
            var manifest = await container.LookupManifestAsync(name, logger, decryptInfo, cancellationToken);
            ClientManifest? clientManifest = null;
            if (manifest != null && includeClientManifest && manifest.IsLiveArchive)
            {
                clientManifest = await container.GetClientManifestAsync(manifest, logger, decryptInfo, cancellationToken);
            }
            return new AssetDetails(name, container, manifest, clientManifest, outputManifestname, decryptInfo);
        }
    }
}
