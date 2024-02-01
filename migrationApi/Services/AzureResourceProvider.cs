using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Resources;

namespace AMSMigrate.Ams
{
    public class AzureResourceProvider
    {
        protected readonly ResourceGroupResource _resourceGroup;
        protected readonly ArmClient _armClient;

        public AzureResourceProvider(TokenCredential credential, string subscriptionId, string resourceGroup)
        {
            _armClient = new ArmClient(credential);
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                subscriptionId,
                resourceGroup);
            _resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
        }

        public async Task<MediaServicesAccountResource> GetMediaAccountAsync(string mediaAccountName)
        {
            return await _resourceGroup.GetMediaServicesAccountAsync(
               mediaAccountName);
        }
    }
}
