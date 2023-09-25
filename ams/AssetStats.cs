using AMSMigrate.Contracts;
using AMSMigrate.Transform;

namespace AMSMigrate.Ams
{
    record class AssetStats
    {
        private int _streamable;
        private int _noLocators;
        private int _skipped;
        private int _failed;
        private int _total;
        private int _migrated;
        private int _successful;

        public int Total => _total;

        public int Streamable => _streamable;

        public int Migrated => _migrated;

        public int Successful => _successful;

        public int Failed => _failed;

        public int Skipped => _skipped;

        public int NoLocators => _noLocators;

        public void Update(AnalysisResult result)
        {
            Update(result.Status);
            if (result.IsStreamable)
            {
                Interlocked.Increment(ref _streamable);
            }

            if (result.LocatorIds.Count == 0)
            {
                Interlocked.Increment(ref _noLocators);
            }
        }

        public void Updated(AssetMigrationResult result)
        {
            Update(result.Status);
        }

        public void Update(MigrationResult result)
        {
            Interlocked.Increment(ref _total);
            switch (result.Status)
            {
                case MigrationStatus.Completed:
                    Interlocked.Increment(ref _successful);
                    break;
                case MigrationStatus.Skipped:
                case MigrationStatus.NotMigrated:
                    Interlocked.Increment(ref _skipped);
                    break;
                case MigrationStatus.AlreadyMigrated:
                    Interlocked.Increment(ref _migrated);
                    break;
                case MigrationStatus.Failed:
                default:
                    Interlocked.Increment(ref _failed);
                    break;
            }
        }
    }
}
