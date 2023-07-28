
using AMSMigrate.Decryption;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace AMSMigrate.Pipes
{
    sealed class BlobStream
    {
        private readonly ILogger _logger;
        private readonly BlockBlobClient _blob;
        private readonly StorageEncryptedAssetDecryptionInfo? _decryptInfo;

        public BlobStream(BlobContainerClient container, string blobName, StorageEncryptedAssetDecryptionInfo? decryptInfo, ILogger logger) :
            this(container.GetBlockBlobClient(blobName), decryptInfo, logger)
        {
        }

        public BlobStream(BlockBlobClient blob, StorageEncryptedAssetDecryptionInfo? decryptInfo, ILogger logger)
        {
            _blob = blob;
            _logger = logger;
            _decryptInfo = decryptInfo;
        }

        public async Task DownloadAsync(Stream stream, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Begin downloading {name}", _blob.Name);

            using var aesTransform = AssetDecryptor.GetAesCtrTransform(_decryptInfo, _blob.Name, false);

            if (aesTransform != null)
            {
                await AssetDecryptor.DecryptTo(aesTransform, _blob, stream, cancellationToken);
            }
            else
            {
                await _blob.DownloadToAsync(stream, cancellationToken: cancellationToken);
            }

            _logger.LogDebug("Finished download of {name}", _blob.Name);
        }

        public async Task DownloadAsync(string filePath, CancellationToken cancellationToken)
        {
            using var stream = File.OpenWrite(filePath);
            await DownloadAsync(stream, cancellationToken);
        }

        public async Task UploadAsync(Stream stream, CancellationToken cancellationToken)
        {
            BlobContentInfo info = await _blob.UploadAsync(stream, cancellationToken: cancellationToken);
        }
    }

    class BlobPipe : Pipe
    {
        private readonly BlobStream _blobStream;

        public BlobPipe(string filePath, BlobContainerClient container, StorageEncryptedAssetDecryptionInfo? decryptInfo, ILogger logger, PipeDirection direction = PipeDirection.Out)
            : base(filePath, direction)
        {
            _blobStream = new BlobStream(container, Path.GetFileName(filePath), decryptInfo, logger);
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            await RunAsync(async (stream, token) =>
            {
                await _blobStream.DownloadAsync(stream,  token);
            }, cancellationToken);
        }

        public async Task UploadAsync(CancellationToken cancellationToken)
        {
            await RunAsync(async (stream, token) =>
            {
                await _blobStream.UploadAsync(stream, token);
            }, cancellationToken);
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            await (_direction == PipeDirection.Out ? DownloadAsync(cancellationToken) : UploadAsync(cancellationToken));
        }
    }
}
