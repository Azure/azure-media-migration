namespace AMSMigrate.Contracts
{
    public record StorageOptions(
        string AccountName,
        string StoragePath,
        Packager Packager,
        string PathTemplate,
        string OutputManifest,
        string? Prefix,
        string WorkingDirectory,
        bool CopyNonStreamable,
        bool OverWrite,
        bool SkipMigrated,
        bool BreakOutputLease,
        bool KeepWorkingFolder,
        int SegmentDuration,
        int BatchSize,
        bool _encryptContent,
        Uri? _keyVaultUri)
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
            BatchSize);
}
