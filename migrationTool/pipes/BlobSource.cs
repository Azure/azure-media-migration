
using AMSMigrate.Decryption;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Pipes
{
    sealed class BlobSource : IPipeSource
    {
        private static readonly IReadOnlyDictionary<string, string> ExtensionToFormatMap = new Dictionary<string, string>
        {
            { ".ts", "mpegts" },
            { ".vtt", "webvtt" }
        };

        private readonly ILogger _logger;
        private readonly BlockBlobClient _blob;
        private readonly StorageEncryptedAssetDecryptionInfo? _decryptInfo;

        public BlobSource(BlobContainerClient container, string blobName, StorageEncryptedAssetDecryptionInfo? decryptInfo, ILogger logger) :
            this(container.GetBlockBlobClient(blobName), decryptInfo, logger)
        {
        }

        public BlobSource(BlockBlobClient blob, StorageEncryptedAssetDecryptionInfo? decryptInfo, ILogger logger)
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

        public string GetStreamArguments()
        {
            var extension = Path.GetExtension(_blob.Name);
            if (!ExtensionToFormatMap.TryGetValue(extension, out var format))
            {
                format = "mp4"; // fallback to mp4.
            }
            return $"-f {format}";
        }

        public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            await DownloadAsync(outputStream, cancellationToken);
        }
    }
}
