using AMSMigrate.Ams;
using AMSMigrate.Contracts;
using AMSMigrate.Decryption;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AMSMigrate.Azure
{
    internal class AzureStorageUploader : IFileUploader
    {
        private readonly MigratorOptions _options;
        private readonly ILogger _logger;
        private readonly BlobServiceClient _blobServiceClient;

        private const string MigrateLock = "__migrate";
        private TimeSpan MaximumMigrateTimePerAsset = TimeSpan.FromHours(12);
        private string _leaseId;

        public AzureStorageUploader(
            MigratorOptions options,
            TokenCredential credential, 
            ILogger<AzureStorageUploader> logger)
        {
            _options = options;
            _logger = logger;
            if (!Uri.TryCreate(options.StoragePath, UriKind.Absolute, out var storageUri))
            {
                storageUri = new Uri($"https://{options.StoragePath}.blob.core.windows.net");
            }
            _blobServiceClient = new BlobServiceClient(storageUri, credential);

            _leaseId = Guid.NewGuid().ToString("D");
        }

        public Uri GetDestinationUri(string container, string fileName)
        {
            return new Uri(_blobServiceClient.Uri, $"/{container}/{fileName}");
        }

        public async Task UploadAsync(
            string containerName,
            string fileName,
            Stream content,
            Headers headers,
            IProgress<long> progress,
            CancellationToken cancellationToken = default)
        {
            _logger.LogTrace(
                "Uploading to {fileName} in container {container} of account: {account}...",
                fileName, containerName, _blobServiceClient.AccountName);
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var outputBlob = container.GetBlockBlobClient(fileName);
            var options = new BlobUploadOptions
            {
                ProgressHandler = progress,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = headers.ContentType
                },
                Conditions = new BlobRequestConditions
                {
                    IfNoneMatch = _options.OverWrite ? null : ETag.All
                }
            };
            await outputBlob.UploadAsync(content, options, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Take the source blob, update the content to a destination container.
        /// </summary>
        /// <param name="containerName">The destination container.</param>
        /// <param name="fileName">The output blob in the destination container.</param>
        /// <param name="blob">The source blob.</param>
        /// <param name="aesTransform">The optional AesTransform for source content decryption.</param>
        /// <param name="cancellationToken">The cancellaton token for the async operation.</param>
        /// <returns></returns>
        public async Task UploadBlobAsync(
            string containerName,
            string fileName,
            BlockBlobClient blob,
            AesCtrTransform? aesTransform,
            CancellationToken cancellationToken)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var outputBlob = container.GetBlockBlobClient(fileName);

            if (aesTransform == null)
            {
                var operation = await outputBlob.StartCopyFromUriAsync(blob.Uri, cancellationToken: cancellationToken);
                await operation.WaitForCompletionAsync(cancellationToken);
            }
            else
            {
                // The input asset is encrypted, extract the clear content to the destination container
                // so that the media content can be used after the asset migration without knowing the initial content key.
                var blobStream = new MemoryStream();

                await AssetDecryptor.DecryptTo(aesTransform, blob, blobStream, cancellationToken);

                blobStream.Position = 0;

                var inputBlobHeaders = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);

                var httpHeader = new BlobHttpHeaders
                {
                    ContentType = inputBlobHeaders?.Value.ContentType ?? "application/octet-stream"
                };

                await outputBlob.UploadAsync(blobStream, httpHeader, cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Mark the status on the container for the generated assets.
        /// </summary>
        /// <param name="container">The name of the output container</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task UpdateOutputStatus(
            string containerName,
            CancellationToken cancellationToken)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            var metadata = new Dictionary<string, string>();

            // Mark the asset type as "dmt_generated"
            metadata.Add(AssetMigrationTracker.AssetTypeKey, AssetMigrationResult.AssetType_DmtGenerated);

            await container.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Check if migrator can upload asset files into the specific output folder in storage container.
        /// This is to ensure only one migrator can upload the generated files to the destination place at any given time.
        /// </summary>
        /// <param name="containerName">The name of the output container.</param>
        /// <param name="outputPath">The destination folder for the migrated asset. </param>
        /// <param name="cancellationToken"></param>
        /// <returns>
        ///    true means this migrator can upload the files to the destination folder,
        ///    false means the destination folder is used by another migrator tool at the moment.
        /// </returns>
        public async Task<bool> CanUploadAsync(
            string containerName,
            string outputPath,
            CancellationToken cancellationToken)
        {
            var canUpload = false;
            var retryForAcquire = false;

            var lockBlob = await GetLockBlobAsync(containerName, outputPath, cancellationToken);
            var leaseClient = lockBlob.GetBlobLeaseClient(_leaseId);            

            do
            {
                try
                {
                    retryForAcquire = false;

                    await leaseClient.AcquireAsync(new TimeSpan(-1), cancellationToken: cancellationToken);

                    // Acquired the lease successfully, update the LastModified time with a new empty
                    // list of metadata with the current LeaseId.
                    await lockBlob.SetMetadataAsync(
                                     new Dictionary<string, string>(),
                                     new BlobRequestConditions() { LeaseId = _leaseId }, 
                                     cancellationToken);

                    // Remove old media files that might be uploaded by other migrator tool
                    // so that new migrator tool will upload a whole set of media files.

                    var container = _blobServiceClient.GetBlobContainerClient(containerName);

                    var blobItems = await container.GetBlobsAsync(prefix: outputPath, 
                                                        cancellationToken: cancellationToken
                                                       ).ToListAsync();

                    if (blobItems.Count > 1)
                    {
                        foreach (var bi in blobItems)
                        {
                            if (!bi.Name.EndsWith(MigrateLock))
                            {
                                await container.DeleteBlobAsync(bi.Name, cancellationToken: cancellationToken);
                            }
                        }
                    }

                    canUpload = true;
                }
                catch (RequestFailedException ex)
                {
                    if (ex.ErrorCode == "LeaseAlreadyPresent")
                    {
                        // The lease is held by another instance of the tool.
                        // Double check the last modification time of the lock blob,
                        // If the last modification time was long time ago, it implies the another instance of tool might 
                        // crash or shutdown unexpectedly, it is time to break the lease and re-acquire it.

                        var properties = await lockBlob.GetPropertiesAsync(cancellationToken: cancellationToken);

                        var lastModifiedTime = properties.Value.LastModified;
                        var elapsed = DateTimeOffset.UtcNow - lastModifiedTime;

                        if (elapsed >= MaximumMigrateTimePerAsset)
                        {
                            await leaseClient.BreakAsync(cancellationToken: cancellationToken);

                            _logger.LogTrace(
                                     "The lease for output path {path} under {container} was broken, the last Modified time was {modified}, it has elapsed for {elapsed} seconds.",
                                     outputPath,
                                     containerName,
                                     lastModifiedTime,
                                     elapsed.TotalSeconds);

                            retryForAcquire = true;
                        }
                        else
                        {
                            _logger.LogTrace(
                                     "The lease for output path {path} under {container} is still held by another tool, the last Modified time was {modified}, it has elapsed for {elapsed} seconds.",
                                     outputPath,
                                     containerName,
                                     lastModifiedTime,
                                     elapsed.TotalSeconds);
                        }
                    }
                    else
                    {
                        // Failed to acquire lease with unexpected error code.
                        _logger.LogWarning(
                                     "Failed to acquire the lease for output path {path} under {container}, Error Code: {error}, Message: {message}.",
                                     outputPath,
                                     containerName,
                                     ex.ErrorCode,
                                     ex.Message);
                    }
                }

            } while (retryForAcquire);

            return canUpload;
        }

        /// <summary>
        /// The instance of migrator has finished the work for uploading files into the destination folder,
        /// clean up the status so that the destination folder can be used by another migrator tool.
        /// </summary>
        /// <param name="containerName">The name of the output container.</param>
        /// <param name="outputPath">The destination folder for the migrated asset. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task UploadCleanupAsync(
            string containerName,
            string outputPath,
            CancellationToken cancellationToken)
        {
            var lockBlob = await GetLockBlobAsync(containerName, outputPath, cancellationToken);

            // The lease is guaranteed to hold by this tool, the migration work for the asset
            // has done, no matter succeed or failed,
            //
            // It is safe to delete the lease-detection blob now.

            await lockBlob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots,
                                       new BlobRequestConditions() { LeaseId = _leaseId },
                                       cancellationToken);

            _logger.LogTrace("The lease-detect blob for output path {path} under {container} is deleted.",
                             outputPath,
                             containerName);
        }

        private async Task<BlockBlobClient> GetLockBlobAsync(
            string containerName,
            string outputPath,
            CancellationToken cancellationToken)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var blobName = outputPath;

            if (!outputPath.EndsWith("/"))
            {
                blobName += "/";
            }

            blobName += MigrateLock;

            var lockBlob = container.GetBlockBlobClient(blobName);

            var exists = await lockBlob.ExistsAsync(cancellationToken);

            if (!exists.Value)
            {
                var content = Encoding.UTF8.GetBytes("Lock");
                await lockBlob.UploadAsync(new MemoryStream(content), cancellationToken: cancellationToken);
            }

            return lockBlob;
        }
    }
}
