namespace AMSMigrate.Contracts
{
    /// <summary>
    /// It holds the options for cleanup commands.
    /// </summary>
    public record CleanupOptions(
      string AccountName,
      CleanupType CleanupType,
      string? ResourceFilter, 
      bool IsForceCleanUpAsset,
      bool IsCleanUpAccount
    );

    public enum CleanupType { 
        StorageAccount,
        AMSAccount
    };
}

