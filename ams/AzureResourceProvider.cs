
using AMSMigrate.Contracts;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Resources;
using Azure.Storage.Blobs;

namespace AMSMigrate.Ams
{
    class AzureResourceProvider
    {
        protected readonly ResourceGroupResource _resourceGroup;
        protected readonly GlobalOptions _globalOptions;
        protected readonly TokenCredential _credentials;

        public AzureResourceProvider(TokenCredential credential, GlobalOptions options)
        {
            _globalOptions = options;
            _credentials = credential;
            var armClient = new ArmClient(credential);
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                options.SubscriptionId,
                options.ResourceGroup);
            _resourceGroup = armClient.GetResourceGroupResource(resourceGroupId);
        }

        public async Task<MediaServicesAccountResource> GetMediaAccountAsync(
            CancellationToken cancellationToken)
        {
            return await _resourceGroup.GetMediaServicesAccountAsync(
                _globalOptions.AccountName, cancellationToken);
        }

        public async Task<(BlobServiceClient, ResourceIdentifier)> GetStorageAccount(CancellationToken cancellationToken)
        {
            StorageAccountResource storage = 
                await _resourceGroup.GetStorageAccountAsync(_globalOptions.AccountName, cancellationToken: cancellationToken);
            var uri = storage.Data.PrimaryEndpoints.BlobUri!;
            return (new BlobServiceClient(uri, _credentials), storage.Id);
        }
    }
}
