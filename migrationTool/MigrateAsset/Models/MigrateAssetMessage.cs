using System.Text.Json.Serialization;

namespace MigrateAsset.models
{
    internal class MigrateAssetMessage
    {
        [JsonRequired]
        public string SubscriptionId { get; set; } = string.Empty;

        [JsonRequired]
        public string ResourceGroup { get; set; } = string.Empty;

        [JsonRequired]
        public string SourceStorageAccountName { get; set; } = string.Empty;

        [JsonRequired]
        public string TargetStorageAccountName { get; set; } = string.Empty;

        [JsonRequired]
        public string AssetName { get; set; } = string.Empty;

    }
}
