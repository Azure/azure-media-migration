namespace AMSMigrate.Contracts
{
    public record StorageOptions(
        string AccountName,
        string StoragePath,
        Packager Packager,
        string PathTemplate,
        string? Prefix,
        string WorkingDirectory,
        bool CopyNonStreamable,
        bool OverWrite,
        bool MarkCompleted,
        bool SkipMigrated,
        bool DeleteMigrated,
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
            MarkCompleted,
            SkipMigrated,
            DeleteMigrated,
            SegmentDuration,
            BatchSize);
}
