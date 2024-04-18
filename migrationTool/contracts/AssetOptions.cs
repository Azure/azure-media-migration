namespace AMSMigrate.Contracts
{
    public record AssetOptions(
        string AccountName,
        string StoragePath,
        Packager Packager,
        string PathTemplate,
        string OutputManifest,
        DateTimeOffset? CreationTimeStart,
        DateTimeOffset? CreationTimeEnd,
        string? ResourceFilter,
        string WorkingDirectory,
        bool CopyNonStreamable,
        bool OverWrite,
        bool SkipMigrated,
        bool BreakOutputLease,
        bool KeepWorkingFolder,
        int SegmentDuration,
        int BatchSize,
        bool OnlyAssetsWithAlternateId)
        : MigratorOptions(
            AccountName,
            StoragePath,
            Packager,
            PathTemplate,
            OutputManifest,
            WorkingDirectory,
            CopyNonStreamable,
            OverWrite,
            SkipMigrated,
            BreakOutputLease,
            KeepWorkingFolder,
            SegmentDuration,
            BatchSize,
            OnlyAssetsWithAlternateId);
}
