namespace AMSMigrate.Contracts
{
    /// <summary>
    /// It holds the options for cleanup commands.
    /// </summary>
    public record CleanupOptions(
      string AccountName,
      string? ResourceFilter, 
      bool IsForceCleanUpAsset,
      bool IsCleanUpAccount
    );

}

