
namespace AMSMigrate.Contracts
{
    /// <summary>
    /// It holds the options for cleanup commands.
    /// </summary>
    public record ResetOptions(
      string AccountName,
      string category
    );

}
