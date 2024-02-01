namespace migrationApi.Models
{
    public class MigrationMessage
    {
        public string SubscriptionId { get; set; }

        public string ResourceGroup { get; set; }

        public string SourceStorageAccountName { get; set; }

        public string TargetStorageAccountName { get; set; }

        public string AssetName { get; set; }
    }
}
