//-----------------------------------------------------------------------
// <copyright company="Microsoft">
//     Copyright (C) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Threading;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace AMSMigrate.Decryption
{
    /// <summary>
    /// Helper class to decrypt asset blobs when the asset was uploaded with storeage-encryption enabled.
    /// </summary>
    public static class AssetDecryptor
    {
        /// <summary>
        /// Get an new instance of AesCtrTransform for a specific blob of an asset or a track of the LiveToVOD asset.
        /// </summary>
        /// <param name="storageDecryptInfo">The decryption information for the asset.</param>
        /// <param name="assetFileName">The asset file name, it could be for a single blob or a media track.</param>
        /// <param name="forTrack">true means it is for a media track.</param>
        /// <returns>The instance of AesCtrTransform.</returns>
        public static AesCtrTransform? GetAesCtrTransform(StorageEncryptedAssetDecryptionInfo? storageDecryptInfo, string assetFileName, bool forTrack = false)
        {
            AesCtrTransform? aesTransform = null;

            if (storageDecryptInfo != null)
            {
                foreach (var meta in storageDecryptInfo.AssetFileEncryptionMetadata)
                {
                    bool hasIv = false;

                    if (forTrack)
                    {
                        // The assetFileName is for a media track, it is the prefix of all fragblob's blob name.
                        // All the Fragblobs for a specific track share the same IV.
                        if (assetFileName.StartsWith(meta.AssetFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            hasIv = true;
                        }
                    }
                    else
                    {
                        // This is a single asset file inside the root folder of asset container.
                        // such as .mp4, .ism, .ismc etc.

                        if (assetFileName.Equals(meta.AssetFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            hasIv = true;
                        }
                    }

                    if (hasIv)
                    {
                        var initializationVector = Convert.ToUInt64(meta.InitializationVector);

                        using (var aesProvider = Aes.Create())
                        {
                            aesProvider.Mode = CipherMode.ECB;
                            aesProvider.Padding = PaddingMode.None;

                            aesProvider.Key = storageDecryptInfo.Key;

                            var transform = aesProvider.CreateEncryptor();

                            aesTransform = new AesCtrTransform(transform, initializationVector, 0);
                        }

                        break;
                    }
                }
            }

            return aesTransform;
        }

        /// <summary>
        /// Download the blob and decrypt it to an output stream.
        /// </summary>
        /// <param name="aesTransform">The AesCtrTransform for the decryption of the source blob. </param>
        /// <param name="sourceBlob">The source blob.</param>
        /// <param name="outputStream">The output stream that holds the decrypted content.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        public static async Task DecryptTo(AesCtrTransform aesTransform, BlockBlobClient sourceBlob, Stream outputStream, CancellationToken cancellationToken)
        {
            using BlobDownloadStreamingResult result = await sourceBlob.DownloadStreamingAsync(cancellationToken: cancellationToken);

            using (CryptoStream cryptoStream = new CryptoStream(result.Content, aesTransform, CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(outputStream);
            }
        }

        /// <summary>
        /// Download the blob and decrypt it to an output file.
        /// </summary>
        /// <param name="aesTransform">The AesCtrTransform for the decryption of the source blob. </param>
        /// <param name="sourceBlob">The source blob. </param>
        /// <param name="outputFilePath">The output file path that holds the decrypted content.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task DecryptTo(AesCtrTransform aesTransform, BlockBlobClient sourceBlob, string outputFilePath, CancellationToken cancellationToken)
        {
            using BlobDownloadStreamingResult result = await sourceBlob.DownloadStreamingAsync(cancellationToken: cancellationToken);

            using (FileStream fileStream = new FileStream(outputFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                using (CryptoStream cryptoStream = new CryptoStream(result.Content, aesTransform, CryptoStreamMode.Read))
                {
                    cryptoStream.CopyTo(fileStream);
                    fileStream.Flush();
                }
            }
        }
    }
}
