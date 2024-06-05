using AMSMigrate.Contracts;
using AMSMigrate.Decryption;
using AMSMigrate.Fmp4;
using AMSMigrate.Transform;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AMSMigrate.Pipes
{
    // A stream of media track that is spread across multiple files.
    public class MultiFileStream : IPipeSource
    {
        private readonly BlobContainerClient _container;
        private readonly ILogger _logger;
        private readonly MediaStream _track;
        private readonly string _trackPrefix;
        private readonly bool _isCloseCaption;
        private readonly StorageEncryptedAssetDecryptionInfo? _decryptInfo;
        private ulong _startingTfdt = 0;
        private bool _firstTfdt = true;
        private readonly LiveArchiveStreamInfo _liveArchiveStreamInfo;

        public MultiFileStream(
            BlobContainerClient container,
            LiveArchiveStreamInfo liveArchiveStreamInfo,
            Track track,
            ClientManifest manifest,
            StorageEncryptedAssetDecryptionInfo? decryptInfo,
            ILogger logger)
        {
            _container = container;
            _logger = logger;
            (_track, _) = manifest.GetStream(track);
            _trackPrefix = track.Source;
            _decryptInfo = decryptInfo;
            _liveArchiveStreamInfo = liveArchiveStreamInfo;

            _isCloseCaption = _track.Type == StreamType.Text && _track.SubType == "SUBT";
        }

        public async Task DownloadAsync(Stream stream, CancellationToken cancellationToken)
        {
            string? chunkName = null;
            try
            {
                _logger.LogDebug("Begin downloading track: {name}", _trackPrefix);

                BlockBlobClient blob;
                BlockBlobClient? lastBlob = null;

                if (!_isCloseCaption)
                {
                    // Header blob is needed only for audio/video cmaf.
                    chunkName = $"{_trackPrefix}/header";
                    blob = _container.GetBlockBlobClient(chunkName);
                    await DownloadClearBlobContent(blob, stream, cancellationToken);
                }
                else
                {
                    byte[] webvttBytes = Encoding.UTF8.GetBytes("WEBVTT");
                    using (MemoryStream headerStream = new MemoryStream(webvttBytes))
                    {
                        headerStream.CopyTo(stream);
                    }
                }

                // Report progress every 10%.
                var i = 0;
                var increment = _track.ChunkCount / 10;

                foreach (var chunk in _track.GetChunks())
                {
                    ++i;
                    if (i % increment == 0)
                    {
                        _logger.LogDebug("Downloaded {i} of total {total} blobs for track {stream}", i, _track.ChunkCount, _trackPrefix);
                    }

                    chunkName = $"{_trackPrefix}/{chunk}";
                    blob = _container.GetBlockBlobClient(chunkName);
                    if (await blob.ExistsAsync(cancellationToken))
                    {
                        _logger.LogTrace("Downloading Chunk for stream: {name} time={time}", _trackPrefix, chunk);
                        await DownloadClearBlobContent(blob, stream, cancellationToken);
                        lastBlob = blob;
                    }
                    else
                    {
                        _logger.LogWarning("Missing Chunk at time {time} for stream {stream}. Ignoring gap by skipping to next.", chunk, _trackPrefix);
                        if (lastBlob is not null)
                        {
                            await DownloadClearBlobContent(lastBlob, stream, cancellationToken);
                        }
                    }
                }
                _logger.LogDebug("Finished downloading track {prefix}", _trackPrefix);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download chunk {chunkName} for live stream: {name}. Error: {ex}", chunkName, _trackPrefix, ex);
                throw;
            }
        }

        /// <summary>
        /// A helper function to generate a cmaf-fragment from the input fragblob which might be in fmp4 format.
        /// </summary>
        /// <param name="inputBoxes">A list of boxes for an input fragment.</param>
        /// <param name="mp4Writer">The writer for the output stream. </param>
        private void GenerateCmafFragment(IList<Box> inputBoxes, MP4Writer mp4Writer)
        {
            if (inputBoxes.Count < 2)
            {
                throw new ArgumentException("A live fragment must have at least 2 mp4-boxes.", nameof(inputBoxes));
            }
            else if (inputBoxes.Count == 2)
            {
                // This must be for a fragment generated by regular LiveChannels
                // or a close caption fragment generated by either regular live channel or UltraLowLatency channel.
                //
                // It should contain two boxes, moof + mdat.
                //
                var moofBox = inputBoxes[0] as moofBox;
                var mdatBox = inputBoxes[1] as mdatBox;

                if (moofBox == null || mdatBox == null)
                {
                    throw new ArgumentException("A live fragment must contain moof box and mdat box.", nameof(inputBoxes));
                }

                var fragment = new Fmp4Fragment(moofBox, mdatBox);

                ulong tfdt = fragment.ReplaceTfxdWithTfdt(_firstTfdt, _startingTfdt);
                if (_firstTfdt)
                {
                    _firstTfdt = false;
                    _startingTfdt = tfdt;
                }

                fragment.WriteTo(mp4Writer);
            }
            else
            {
                // This must be for a video or audio fragment generated by UltraLowLatency live channels.
                //
                // Each fragment contains:
                //      styp + { [prft] + moof + mdat }

                if (inputBoxes[0].Type != MP4BoxType.styp)
                {
                    throw new ArgumentException("An ultra low-latency live fragment must start with styp box.", nameof(inputBoxes));
                }

                // The rest of boxes are in CMAF format already,
                // Ignore the optional prft box and take the rest into the output stream.
                for (var i = 1; i < inputBoxes.Count; i++)
                {
                    var box = inputBoxes[i];

                    if (box.Type != MP4BoxType.prft)
                    {
                        inputBoxes[i].WriteTo(mp4Writer);
                    }
                }
            }
        }

        /// <summary>
        /// A helper function to generate VTT text for a specific fragblob
        /// </summary>
        /// <param name="inputBoxes">A list of boxes for an close caption fragblob.</param>
        /// <param name="mp4Writer">The writer for the output stream. </param>
        private void GenerateVttContent(IList<Box> inputBoxes, MP4Writer mp4Writer)
        {
            if (inputBoxes.Count != 2)
            {
                throw new ArgumentException("A live fragment for close caption must contain two mp4-boxes.", nameof(inputBoxes));
            }

            var moofBox = inputBoxes[0] as moofBox;
            var mdatBox = inputBoxes[1] as mdatBox;

            if (moofBox == null || mdatBox == null)
            {
                throw new ArgumentException("A live fragment must contain moof box and mdat box.", nameof(inputBoxes));
            }

            var ttmlText = mdatBox.SampleData;
            try
            {
                long offsetInMs = _liveArchiveStreamInfo.VideoStartTime * 1000 / _liveArchiveStreamInfo.VideoTimeScale;
                // Call API to convert ttmlText to VTT text.
                var vttText = VttConverter.ConvertTTMLtoVTT(ttmlText, offsetInMs);

                if (vttText != null)
                {
                    mp4Writer.Write(vttText);
                }
                else
                {
                    _logger.LogTrace("vttText is null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting TTML to VTT.");
            }
        }

        private async Task DownloadClearBlobContent(BlockBlobClient sourceBlob, Stream outputStream, CancellationToken cancellationToken)
        {
            using var tmpStream = new MemoryStream();

            using var aesTransform = AssetDecryptor.GetAesCtrTransform(_decryptInfo, _trackPrefix, true);

            if (aesTransform == null)
            {
                await sourceBlob.DownloadToAsync(tmpStream, cancellationToken);
            }
            else
            {
                await AssetDecryptor.DecryptTo(aesTransform, sourceBlob, tmpStream, cancellationToken);
            }

            tmpStream.Position = 0;

            var reader = new MP4Reader(tmpStream);
            var boxes = new List<Box>();

            while (tmpStream.Position < tmpStream.Length)
            {
                var box = MP4BoxFactory.ParseSingleBox(reader);

                boxes.Add(box);
            }

            using var generatedStream = new MemoryStream();
            var writer = new MP4Writer(generatedStream); // Don't dispose, that will close the stream.

            if (sourceBlob.Name.EndsWith("/header"))
            {
                // It is a header blob which contains ftyp, moov and optional uuid box for the stream manifest.
                foreach (var box in boxes)
                {
                    // Ignore the UUID box for the stream manifest which is not recognized by shaka and ffmpeg packager.
                    if (box.ExtendedType == null)
                    {
                        box.WriteTo(writer);
                    }
                }
            }
            else
            {
                if (_isCloseCaption)
                {
                    GenerateVttContent(boxes, writer);
                }
                else
                {
                    // It is for a fragment generated by a live channel.
                    // Generate cmaf fragment from the input stream.
                    GenerateCmafFragment(boxes, writer);
                }
            }

            writer.Flush();
            generatedStream.Position = 0; // Rewind stream

            generatedStream.CopyTo(outputStream);
        }

        public string GetStreamArguments() => string.Empty;

        public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            await DownloadAsync(outputStream, cancellationToken);
        }
    }
}
