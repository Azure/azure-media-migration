using Azure.ResourceManager.Media.Models;
using Azure.ResourceManager.Media;
using Azure.Storage.Blobs;
using Azure;

namespace AMSMigrate.Ams
{
    static class AmsExtensions
    {
        public static async Task<BlobContainerClient> GetContainer(this MediaAssetResource asset, CancellationToken cancellationToken)
        {
            var content = new MediaAssetStorageContainerSasContent
            {
                Permissions = MediaAssetContainerPermission.ReadWriteDelete,
                ExpireOn = DateTimeOffset.Now.AddHours(1)
            };

            var uris = asset.GetStorageContainerUrisAsync(content, cancellationToken)
                .AsPages();
            var urls = new List<Uri>();
            await foreach (var page in uris)
            {
                urls.AddRange(page.Values);
            }

            return new BlobContainerClient(urls[0]);
        }

        public static async Task<StreamingLocatorResource?> GetStreamingLocatorAsync(
            this MediaServicesAccountResource account,
            MediaAssetResource asset,
            CancellationToken cancellationToken)
        {
            var locators = asset.GetStreamingLocatorsAsync(cancellationToken);
            await foreach (var locatorData in locators)
            {
                StreamingLocatorResource locator = await account.GetStreamingLocatorAsync(locatorData.Name, cancellationToken);
                return locator;
            }
            return null;
        }

        public static async Task<string> GetStreamingEndpointAsync(
            this MediaServicesAccountResource account,
            string endpointName = "default",
            CancellationToken cancellationToken = default)
        {
            StreamingEndpointResource endpoint = await account.GetStreamingEndpointAsync(endpointName, cancellationToken);
            return endpoint.Data.HostName;
        }

        public static async Task CreateStreamingLocator(
            this MediaServicesAccountResource account,
            MediaAssetResource asset,
            CancellationToken cancellationToken)
        {
            await account.GetStreamingPolicies().CreateOrUpdateAsync(WaitUntil.Completed, "migration", new StreamingPolicyData
            {
                NoEncryptionEnabledProtocols = new MediaEnabledProtocols(
                    isDashEnabled: true,
                    isHlsEnabled: true,
                    isDownloadEnabled: false,
                    isSmoothStreamingEnabled: false)
            });
            await account.GetStreamingLocators().CreateOrUpdateAsync(WaitUntil.Completed, "migration", new StreamingLocatorData
            {
                AssetName = asset.Data.Name,
                StreamingPolicyName = "migration"
            });
        }
    }
}
