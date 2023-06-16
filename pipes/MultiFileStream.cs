using AMSMigrate.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace AMSMigrate.Pipes
{
    // A stream of media track that is spread across multiple files.
    public class MultiFileStream
    {
        private readonly BlobContainerClient _container;
        private readonly ILogger _logger;
        private readonly MediaStream _track;
        private readonly string _trackPrefix;

        public MultiFileStream(
            BlobContainerClient container,
            MediaStream track,
            string trackPrefix,
            ILogger logger)
        {
            _container = container;
            _logger = logger;
            _track = track;
            _trackPrefix = trackPrefix;
        }

        public async Task DownloadAsync(Stream stream, CancellationToken cancellationToken)
        {
            string? chunkName = null;
            try
            {
                _logger.LogDebug("Begin downloading track: {name}", _trackPrefix);
                chunkName = $"{_trackPrefix}/header";
                var blob = _container.GetBlockBlobClient(chunkName);
                await blob.DownloadToAsync(stream, cancellationToken);

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
                        await blob.DownloadToAsync(stream, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Missing Chunk at time {time} for stream {stream}. Ignoring gap by skipping to next.", chunk, _trackPrefix);
                    }
                }
                _logger.LogDebug("Finished downloading track {prefix}", _trackPrefix);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download chunk {chunkName} for live stream: {name}. Error: {ex}", chunkName, _trackPrefix , ex);
                throw;
            }
        }
    }

    //Writes a multi file track to a pipe stream.
    internal class MultiFilePipe : Pipe
    {
        private readonly MultiFileStream _multiFileStream;

        public MultiFilePipe(
            string filePath,
            MultiFileStream multiFileStream)
            : base(filePath, PipeDirection.Out)
        {
            _multiFileStream = multiFileStream;
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            await RunAsync(async (stream, token) =>
            {
                await _multiFileStream.DownloadAsync(stream, token);
            }, cancellationToken);
        }

        public override string GetStreamArguments()
        {
            return "-f mp4 -seekable 0";
        }

        public override async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            await _multiFileStream.DownloadAsync(stream, cancellationToken);
        }
        public async Task DownloadAsync(string path, CancellationToken cancellationToken)
        {
            using var file = File.OpenWrite(path);
            await _multiFileStream.DownloadAsync(file, cancellationToken);
        }
    }
}
