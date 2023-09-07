
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

        public Dictionary<string, ResourceGroupResource> resourceGroups { get; set; }

        public AzureResourceProvider(TokenCredential credential, GlobalOptions options)
        {
            _globalOptions = options;
            _credentials = credential;
            var clientOptions = new ArmClientOptions();
            clientOptions.Diagnostics.ApplicationId = $"AMSMigrate/{GetType().Assembly.GetName().Version}";
            var armClient = new ArmClient(credential, default, clientOptions);
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                options.SubscriptionId,
                options.ResourceGroup);
            _resourceGroup = armClient.GetResourceGroupResource(resourceGroupId);
            resourceGroups = new Dictionary<string, ResourceGroupResource>();
        }

        public async Task SetResourceGroupsAsync(MediaServicesAccountResource account, CancellationToken cancellationToken)
        {
            var armDics = await GetResourceGroupNameAsync(account, cancellationToken);
            if (armDics != null)
            {
                foreach (var arm in armDics)
                {
                    var clientOptions = new ArmClientOptions();
                    clientOptions.Diagnostics.ApplicationId = $"AMSMigrate/{GetType().Assembly.GetName().Version}";
                    var armClient = new ArmClient(_credentials, default, clientOptions);
                    var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                        _globalOptions.SubscriptionId,
                        arm.Value);
                    var _resourceGroup = armClient.GetResourceGroupResource(resourceGroupId);
                    resourceGroups.Add(arm.Key, _resourceGroup);
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

        public async Task<BlobServiceClient?> GetStorageAccountAsync(
            MediaServicesAccountResource account,
            MediaAssetResource asset,
            CancellationToken cancellationToken)
        {
            string assetStorageAccountName = asset.Data.StorageAccountName;
            if (resourceGroups.TryGetValue(asset.Data.StorageAccountName, out var rg))
            {
                var resource = await rg.GetStorageAccountAsync(asset.Data.StorageAccountName, cancellationToken: cancellationToken);
                return GetStorageAccount(resource);
            }
            return null;
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

        private async Task<Dictionary<string, string>> GetResourceGroupNameAsync(MediaServicesAccountResource account, CancellationToken cancellationToken)
        {
            IList<MediaServicesStorageAccount> storageAccounts;
            Dictionary<string, string> armDics = new Dictionary<string, string>();
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

                        if (!string.IsNullOrEmpty(storageAccountId))
                        {
                            parts = storageAccountId.Split('/');
                            string resourceGroupName = parts[parts.Length - 5];
                            string storageAccName = parts[parts.Length - 1];
                            armDics.Add(storageAccName, resourceGroupName);
                        }
                    }
                }

            }
            return armDics;
        }

    }
}
