using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Local
{
    internal class LocalFileUploader : IFileUploader
    {
        private readonly AssetOptions _assetOptions;
        private readonly ILogger _logger;

        public LocalFileUploader(AssetOptions options, ILogger<LocalFileUploader> logger)
        {
            _assetOptions = options;
            _logger = logger;
            Directory.CreateDirectory(_assetOptions.StoragePath);
        }

        public Uri GetDestinationUri(string container, string fileName)
        {
            return new Uri(Path.Combine(_assetOptions.StoragePath, container, fileName));
        }

        public async Task UploadAsync(string container, string fileName, Stream content, IProgress<long> progress, CancellationToken cancellationToken)
        {
            var subDir = Path.GetDirectoryName(fileName) ?? string.Empty;
            if (subDir[0] == '/' || subDir[0] == '\\')
            {
                subDir = subDir.Substring(1);
            }

            var baseDir = Path.Combine(_assetOptions.StoragePath, container, subDir);
            if (!Directory.Exists(baseDir)) 
            {
                Directory.CreateDirectory(baseDir);
            }

            using var file = File.OpenWrite(Path.Combine(baseDir, Path.GetFileName(fileName)));
            await content.CopyToAsync(file, cancellationToken);
        }
    }
}
