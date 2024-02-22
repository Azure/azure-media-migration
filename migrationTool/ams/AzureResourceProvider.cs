using AMSMigrate.Contracts;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.Storage.Blobs;

namespace AMSMigrate.Ams
{
    class AzureResourceProvider
    {
        protected readonly ResourceGroupResource _resourceGroup;
        protected readonly GlobalOptions _globalOptions;
        protected readonly TokenCredential _credentials;
        protected readonly ArmClient _armClient;

        private Dictionary<string, StorageAccountResource> _storageAccountResources;

        public AzureResourceProvider(TokenCredential credential, GlobalOptions options)
        {
            _globalOptions = options;
            _credentials = credential;
            var clientOptions = new ArmClientOptions();
            clientOptions.Diagnostics.ApplicationId = $"AMSMigrate/{GetType().Assembly.GetName().Version}";
            _armClient = new ArmClient(credential, default, clientOptions);
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                options.SubscriptionId,
                options.ResourceGroup);
            _resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
            _storageAccountResources = new Dictionary<string, StorageAccountResource>();
        }

        public async Task SetStorageAccountResourcesAsync(MediaServicesAccountResource account, CancellationToken cancellationToken)
        {
            IList<MediaServicesStorageAccount> storageAccounts;
            var mediaServiceResource = await account.GetAsync(cancellationToken: cancellationToken);
            if (mediaServiceResource.GetRawResponse().Status == 200)
            {
                storageAccounts = mediaServiceResource.Value.Data.StorageAccounts;

                foreach (var storageAccount in storageAccounts)
                {
                    var storageAccountId = storageAccount.Id;
                    var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                            storageAccountId.SubscriptionId!,
                            storageAccountId.ResourceGroupName!);
                    var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
                    StorageAccountResource storage = await resourceGroup.GetStorageAccountAsync(storageAccountId.Name,
                        cancellationToken: cancellationToken);
                    _storageAccountResources.Add(storageAccountId.Name, storage);
                }
            }
        }

        public async Task SetStorageAccountResourcesAsync(string storageAccountName, CancellationToken cancellationToken)
        {
            StorageAccountResource storage = await _resourceGroup.GetStorageAccountAsync(storageAccountName,
                cancellationToken: cancellationToken);
            _storageAccountResources.Add(storageAccountName, storage);
        }

        public async Task<MediaServicesAccountResource> GetMediaAccountAsync(
            string mediaAccountName,
            CancellationToken cancellationToken)
        {
            return await _resourceGroup.GetMediaServicesAccountAsync(
               mediaAccountName, cancellationToken);
        }

        public BlobServiceClient GetBlobServiceClient(MediaAssetResource asset)
        {
            string assetStorageAccountName = asset.Data.StorageAccountName;
            if (_storageAccountResources.TryGetValue(assetStorageAccountName, out var resource))
            {
                return GetStorageAccount(resource);
            }
            throw new Exception($"Failed to get BlobServiceClient for storage account {assetStorageAccountName}.");
        }

        public (BlobServiceClient, ResourceIdentifier) GetBlobServiceClient(string storageAccountName)
        {
            string assetStorageAccountName = storageAccountName;
            if (_storageAccountResources.TryGetValue(assetStorageAccountName, out var resource))
            {
                return (GetStorageAccount(resource), resource.Id);
            }
            throw new Exception($"Failed to get BlobServiceClient for storage account {assetStorageAccountName}.");
        }

        private BlobServiceClient GetStorageAccount(StorageAccountResource storage)
        {
            var uri = storage.Data.PrimaryEndpoints.BlobUri!;
            return new BlobServiceClient(uri, _credentials);
        }
    }
}
