using AMSMigrate.Azure;
using AMSMigrate.Contracts;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace AMSMigrate.azure
{
    internal class AzureProvider : ICloudProvider
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly TokenCredential _credentials;

        public AzureProvider(
            ILoggerFactory loggerFactory,
            TokenCredential credentials,
            KeyOptions keyOptions) 
        {
            _credentials = credentials;
            _loggerFactory = loggerFactory;
        }

        public IFileUploader GetStorageProvider(AssetOptions assetOptions) 
            => new AzureStorageUploader(assetOptions, _credentials, _loggerFactory.CreateLogger<AzureStorageUploader>());

        public ISecretUploader GetSecretProvider(KeyOptions keyOptions) 
            => new KeyVaultUploader(keyOptions, _credentials, _loggerFactory.CreateLogger<KeyVaultUploader>());
    }
}
