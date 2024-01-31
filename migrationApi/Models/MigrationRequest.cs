using System.Text.Json.Serialization;

namespace migrationApi.Models
{
    public class MigrationRequest
    {
        [JsonPropertyName("subscriptionId")]
        public string SubscriptionId { get; set; }

        [JsonPropertyName("resourceGroup")]
        public string ResourceGroup { get; set; }

        [JsonPropertyName("mediaServiceName")]
        public string MediaServiceName { get; set; }

        [JsonPropertyName("targetStorageAccountName")]
        public string TargetStorageAccountName { get; set; }

        [JsonPropertyName("assetName")]
        public string AssetName { get; set; }
    }
}
