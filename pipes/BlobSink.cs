using AMSMigrate.Contracts;
using Azure.Storage.Blobs.Specialized;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Pipes
{
    internal class BlobSink : IPipeSink
    {
        private readonly BlockBlobClient _blobClient;

        public BlobSink(BlockBlobClient blobClient)
        {
            _blobClient = blobClient;
        }

        // The ffmpeg format is dash.
        public string GetFormat() => "dash";

        public async Task ReadAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            await _blobClient.UploadAsync(inputStream, cancellationToken: cancellationToken);
        }
    }

    internal class UploadSink : IPipeSink
    {
        private readonly IFileUploader _uploader;
        private readonly string _container;
        private readonly string _filename;
        private readonly ILogger _logger;

        public UploadSink(IFileUploader uploader, string container, string filename, ILogger logger)
        {
            _uploader = uploader;
            _container = container;
            _filename = filename;
            _logger = logger;
        }

        // The ffmpeg format is dash.
        public string GetFormat() => "dash";

        public async Task ReadAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            try
            {
                var progress = new Progress<long>();
                await _uploader.UploadAsync(_container, _filename, inputStream, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to upload {blob}; {ex}", _filename, ex);
                throw;
            }
        }

        public async Task UploadAsync(string filename, IProgress<long> progress, CancellationToken cancellationToken)
        {
            using var file = File.OpenRead(filename);
            await _uploader.UploadAsync(_container, _filename, file, progress, cancellationToken);
        }
    }

}
