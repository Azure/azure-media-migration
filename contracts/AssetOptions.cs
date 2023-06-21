﻿namespace AMSMigrate.Contracts
{
    public record AssetOptions(
        string StorageAccount,
        Packager Packager,
        string PathTemplate,
        string WorkingDirectory,
        bool CopyNonStreamable,
        bool OverWrite,
        bool MarkCompleted,
        bool SkipMigrated,
        bool DeleteMigrated,
        int SegmentDuration,
        int BatchSize);
}