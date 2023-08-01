using AMSMigrate.Contracts;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.Azure
{
    internal class AzureProvider : ICloudProvider
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly TokenCredential _credentials;

        public AzureProvider(
            ILoggerFactory loggerFactory,
            TokenCredential credentials) 
        {
            _credentials = credentials;
            _loggerFactory = loggerFactory;
        }

        public IFileUploader GetStorageProvider(MigratorOptions migratorOptions) 
            => new AzureStorageUploader(migratorOptions, _credentials, _loggerFactory.CreateLogger<AzureStorageUploader>());

        public ISecretUploader GetSecretProvider(KeyVaultOptions keyOptions) 
            => new KeyVaultUploader(keyOptions, _credentials, _loggerFactory.CreateLogger<KeyVaultUploader>());
    }
}
