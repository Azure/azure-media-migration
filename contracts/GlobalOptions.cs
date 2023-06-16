using Microsoft.Extensions.Logging;

namespace AMSMigrate.Contracts
{
    public record GlobalOptions(
        string SubscriptionId,
        string ResourceGroup,
        string AccountName,
        string? ResourceFilter,
        LogLevel LogLevel,
        string LogFile);

}

