namespace AMSMigrate.Contracts
{
    /// <summary>
    /// It holds the common options for migrating commands, such as "assets" and "storage".
    /// </summary>
    public record MigratorOptions(
        string AccountName,
        string StoragePath,        
        Packager Packager,
        string PathTemplate,
        string WorkingDirectory,
        bool CopyNonStreamable,
        bool OverWrite,
        bool SkipMigrated,
        int SegmentDuration,
        int BatchSize);
}
