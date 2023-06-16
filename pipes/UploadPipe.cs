using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace AMSMigrate.Pipes
{
    class UploadHelper
    {
        private readonly IFileUploader _uploader;
        private readonly string _container;
        public readonly string _storagePrefix;

        public UploadHelper(
            string container,
            string storagePrefix,
            IFileUploader uploader)
        {
            _uploader = uploader;
            _container = container;
            _storagePrefix = storagePrefix;
        }

        public async Task UploadAsync(string filename, Stream content, IProgress<long> progress, CancellationToken cancellationToken)
        {
            var storagePath = _storagePrefix + filename;
            await _uploader.UploadAsync(_container, storagePath, content, progress, cancellationToken);
        }
    }

    sealed class UploadPipe : Pipe
    {
        private readonly ILogger _logger;
        private readonly UploadHelper _helper;
        private readonly string _filename;
        private readonly IProgress<long> _progress;

        public UploadPipe(
            string filePath,
            UploadHelper helper,
            ILogger logger,
            IProgress<long> progress) :
            base(filePath, PipeDirection.In)
        {
            _helper = helper;
            _logger = logger;
            _progress = progress;
            _filename = Path.GetFileName(filePath);
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting upload of {Pipe} to storage {blob}", PipePath, _filename);
            await RunAsync(async (stream, token) =>
            {
                await _helper.UploadAsync(_filename, stream, _progress, token);
            }, cancellationToken);
            _logger.LogDebug("Finished upload of {Pipe} to {file}", PipePath, _filename);
        }
    }
}
