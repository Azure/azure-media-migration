﻿namespace AMSMigrate.Contracts
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
        bool CleanUp,
        int SegmentDuration,
        int BatchSize)
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
            CleanUp,
            SegmentDuration,
            BatchSize);
}
