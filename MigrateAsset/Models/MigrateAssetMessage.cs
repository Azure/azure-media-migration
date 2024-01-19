using System.Text.Json.Serialization;

namespace MigrateAsset.models
{
    internal class MigrateAssetMessage
    {
        [JsonRequired]
        public string SubscriptionId { get; set; }

        [JsonRequired]
        public string ResourceGroup { get; set; }

        [JsonRequired]
        public string MediaServiceName { get; set; }

        [JsonRequired]
        public string TargetStorageAccountName { get; set; }

        [JsonRequired]
        public string AssetName { get; set; }
    }
}
