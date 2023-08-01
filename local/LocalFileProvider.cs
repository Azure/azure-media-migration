using AMSMigrate.Contracts;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Local
{
    internal class LocalFileProvider : ICloudProvider
    {
        private readonly ILoggerFactory _loggerFactory;

        public LocalFileProvider(ILoggerFactory loggerFactory) 
        {
            _loggerFactory = loggerFactory;
        }

        public ISecretUploader GetSecretProvider(KeyVaultOptions keyOptions)
        {
            throw new NotImplementedException();
        }

        public IFileUploader GetStorageProvider(MigratorOptions assetOptions)
        {
            return new LocalFileUploader(assetOptions, _loggerFactory.CreateLogger<LocalFileUploader>());
        }
    }
}
