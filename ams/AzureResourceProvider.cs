
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

        private Dictionary<string, ResourceGroupResource> StorageResourceGroups;

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
            StorageResourceGroups = new Dictionary<string, ResourceGroupResource>();
        }

        public async Task SetStorageResourceGroupsAsync(MediaServicesAccountResource account, CancellationToken cancellationToken)
        {
            IList<MediaServicesStorageAccount> storageAccounts;
            var mediaServiceResource = await account.GetAsync(cancellationToken: cancellationToken);
            if (mediaServiceResource.GetRawResponse().Status == 200)
            {
                storageAccounts = mediaServiceResource.Value.Data.StorageAccounts;
                if (storageAccounts != null && storageAccounts.Any())
                {
                    foreach (var storageAccount in storageAccounts)
                    {
                        string? storageAccountId = storageAccount.Id;
                        string[] parts;
                        ///subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/microsoft.storage/storageaccounts/{accountName}
                        if (!string.IsNullOrEmpty(storageAccountId))
                        {
                            parts = storageAccountId.Split('/');
                            string resourceGroupName = parts[4];
                            string storageAccName = parts[8];
                            string subscriptionId = parts[2];
                            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                                 subscriptionId,
                                 resourceGroupName);
                            var _resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
                            StorageResourceGroups.Add(storageAccName, _resourceGroup);
                       }
                    }
                }

            }
        }

        public async Task<MediaServicesAccountResource> GetMediaAccountAsync(
            string mediaAccountName,
            CancellationToken cancellationToken)
        {
            return await _resourceGroup.GetMediaServicesAccountAsync(
               mediaAccountName, cancellationToken);
       }

        public async Task<BlobServiceClient> GetStorageAccountAsync(
            MediaServicesAccountResource account,
            MediaAssetResource asset,
            CancellationToken cancellationToken)
        {
            string assetStorageAccountName = asset.Data.StorageAccountName;
            StorageResourceGroups.TryGetValue(asset.Data.StorageAccountName, out var rg);
            var resource = await rg.GetStorageAccountAsync(asset.Data.StorageAccountName, cancellationToken: cancellationToken);
            return GetStorageAccount(resource);
        }

        public async Task<(BlobServiceClient, ResourceIdentifier)> GetStorageAccount(string storageAccountName, CancellationToken cancellationToken)
        {
            StorageAccountResource storage =
                await _resourceGroup.GetStorageAccountAsync(storageAccountName, cancellationToken: cancellationToken);
            return (GetStorageAccount(storage), storage.Id);
        }

        private BlobServiceClient GetStorageAccount(StorageAccountResource storage)
        {
            var uri = storage.Data.PrimaryEndpoints.BlobUri!;
            return new BlobServiceClient(uri, _credentials);
        }
    }
}
