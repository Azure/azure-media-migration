using System.Text.Json.Serialization;

namespace migrationApi.Models
{
    public class MigrationRequest
    {
        public string SubscriptionId { get; set; }

        public string ResourceGroup { get; set; }

        public string MediaServiceName { get; set; }

        public string TargetStorageAccountName { get; set; }

        public string AssetName { get; set; }
    }
}
