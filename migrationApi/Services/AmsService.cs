using AMSMigrate.Ams;
using Azure.Core;
using Azure.ResourceManager.Media;

namespace migrationApi.Services
{
    public class AmsService
    {
        private readonly AzureResourceProvider _resourceProvider;

        public AmsService(TokenCredential credential, string subscriptionId, string resourceGroup)
        {
            _resourceProvider = new AzureResourceProvider(credential, subscriptionId, resourceGroup);
        }

        public async Task<MediaAssetCollection> GetAssets(string mediaServicesAccountName)
        {
            var amsAccount = await _resourceProvider.GetMediaAccountAsync(mediaServicesAccountName);
            var assets = amsAccount.GetMediaAssets();

            return assets;
        }
    }
}
