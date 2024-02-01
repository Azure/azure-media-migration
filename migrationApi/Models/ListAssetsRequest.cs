namespace migrationApi.Models
{
    public class ListAssetsRequest
    {
        public string SubscriptionId {  get; set; }

        public string ResourceGroup { get; set; }

        public string AzureMediaServicesAccountName { get; set; }

        public string AssetName { get; set; }
    }
}
