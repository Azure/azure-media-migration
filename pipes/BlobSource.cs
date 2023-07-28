﻿using AMSMigrate.Decryption;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Pipes
{
    internal class BlobSource : IPipeSource
    {
        private static readonly IReadOnlyDictionary<string, string> ExtensionToFormatMap = new Dictionary<string, string>
        {
            { ".ts", "mpegts" },
            { ".vtt", "webvtt" }
        };

        private readonly BlockBlobClient _blobClient;
        private readonly StorageEncryptedAssetDecryptionInfo? _decryptInfo;
        private readonly ILogger _logger;

        public BlobSource(BlockBlobClient blobClient, StorageEncryptedAssetDecryptionInfo? decryptInfo, ILogger logger)
        {
            _blobClient = blobClient;
            _decryptInfo = decryptInfo;
            _logger = logger;
        }

        public string GetStreamArguments()
        {
            var extension = Path.GetExtension(_blobClient.Name);
            if (!ExtensionToFormatMap.TryGetValue(extension, out var format))
            {
                format = "mp4"; // fallback to mp4.
            }
            return $"-f {format}";
        }

        public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Begin downloading track: {name}", _blobClient.Name);
            var options = new BlobDownloadOptions
            {
                ProgressHandler = new Progress<long>(
                    progress =>
                    {
                        _logger.LogTrace("Download progress of blob {blob} is {progress}", _blobClient.Name, progress);
                    })
            };
            using BlobDownloadStreamingResult result = await _blobClient.DownloadStreamingAsync(options, cancellationToken);
            try
            {
                await result.Content.CopyToAsync(outputStream, cancellationToken);
                _logger.LogDebug("Finished downloading track: {name}", _blobClient.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download {blob}", _blobClient.Name);
                throw;
            }
        }

        public async Task DownloadAsync(string filePath, CancellationToken cancellationToken)
        {
            using var aesTransform = AssetDecryptor.GetAesCtrTransform(_decryptInfo, _blobClient.Name, false);

            if (aesTransform != null)
            {
                await AssetDecryptor.DecryptTo(aesTransform, _blobClient, filePath, cancellationToken);
            }
            else
            {
                await _blobClient.DownloadToAsync(filePath, cancellationToken);
            }
        }
    }
}
