using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Resources;

namespace AMSMigrate.Ams
{
    public class AzureResourceProvider
    {
        protected readonly ResourceGroupResource _resourceGroup;
        //protected readonly TokenCredential _credentials;
        protected readonly ArmClient _armClient;

        //private Dictionary<string, ResourceGroupResource> _storageResourceGroups;

        public AzureResourceProvider(TokenCredential credential, string subscriptionId, string resourceGroup)
        {
            //_credentials = credential;
            //var clientOptions = new ArmClientOptions();
            //clientOptions.Diagnostics.ApplicationId = $"AMSMigrate/{GetType().Assembly.GetName().Version}";
            _armClient = new ArmClient(credential);
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                subscriptionId,
                resourceGroup);
            _resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
            //_storageResourceGroups = new Dictionary<string, ResourceGroupResource>();
        }

        //public async Task SetStorageResourceGroupsAsync(MediaServicesAccountResource account, CancellationToken cancellationToken)
        //{
        //    IList<MediaServicesStorageAccount> storageAccounts;
        //    var mediaServiceResource = await account.GetAsync(cancellationToken: cancellationToken);
        //    if (mediaServiceResource.GetRawResponse().Status == 200)
        //    {
        //        storageAccounts = mediaServiceResource.Value.Data.StorageAccounts;

        //        foreach (var storageAccount in storageAccounts)
        //        {
        //            var storageAccountId = storageAccount.Id;
        //            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
        //                    storageAccountId.SubscriptionId!,
        //                    storageAccountId.ResourceGroupName!);
        //            var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
        //            _storageResourceGroups.Add(storageAccountId.Name, resourceGroup);
        //        }
        //    }
        //}

        public async Task<MediaServicesAccountResource> GetMediaAccountAsync(string mediaAccountName)
        {
            return await _resourceGroup.GetMediaServicesAccountAsync(
               mediaAccountName);
        }

        //public async Task<BlobServiceClient> GetStorageAccountAsync(
        //    MediaServicesAccountResource account,
        //    MediaAssetResource asset,
        //    CancellationToken cancellationToken)
        //{
        //    string assetStorageAccountName = asset.Data.StorageAccountName;
        //    _storageResourceGroups.TryGetValue(asset.Data.StorageAccountName, out var rg);
        //    var resource = await rg.GetStorageAccountAsync(asset.Data.StorageAccountName, cancellationToken: cancellationToken);
        //    return GetStorageAccount(resource);
        //}

        //public async Task<(BlobServiceClient, ResourceIdentifier)> GetStorageAccount(string storageAccountName, CancellationToken cancellationToken)
        //{
        //    StorageAccountResource storage =
        //        await _resourceGroup.GetStorageAccountAsync(storageAccountName, cancellationToken: cancellationToken);
        //    return (GetStorageAccount(storage), storage.Id);
        //}

        //private BlobServiceClient GetStorageAccount(StorageAccountResource storage)
        //{
        //    var uri = storage.Data.PrimaryEndpoints.BlobUri!;
        //    return new BlobServiceClient(uri, _credentials);
        //}
    }
}
