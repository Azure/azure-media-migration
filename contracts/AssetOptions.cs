namespace AMSMigrate.Contracts
{
    public record AssetOptions(
        string AccountName,
        string StoragePath,        
        Packager Packager,
        string PathTemplate,
        DateTimeOffset? CreationTimeStart,
        DateTimeOffset? CreationTimeEnd,
        string? ResourceFilter,
        string WorkingDirectory,
        bool CopyNonStreamable,
        bool OverWrite,
        bool SkipMigrated,
        int SegmentDuration,
        int BatchSize)
        : MigratorOptions(
            AccountName,
            StoragePath,
            Packager,
            PathTemplate,
            WorkingDirectory,
            CopyNonStreamable,
            OverWrite,
            SkipMigrated, 
            SegmentDuration,
            BatchSize);
}
