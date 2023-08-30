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
        string OutputManifest,
        string WorkingDirectory,
        bool CopyNonStreamable,
        bool OverWrite,
        bool SkipMigrated,
        bool BreakOutputLease,
        bool KeepWorkingFolder,
        int SegmentDuration,
        int BatchSize)
    {
        public bool EncryptContent { get; set; }

        public string? KeyUri { get; set; }

        public Uri? KeyVaultUri { get; set; }
    }
}
