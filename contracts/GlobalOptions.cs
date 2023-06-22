using Microsoft.Extensions.Logging;

namespace AMSMigrate.Contracts
{
    public record GlobalOptions(
        string SubscriptionId,
        string ResourceGroup,
        string AccountName,
        CloudType CloudType,
        string? ResourceFilter,
        LogLevel LogLevel,
        string LogDirectory)
    {
        private readonly string _logFile = $"MigrationLog_{DateTime.Now:HH_mm_ss}.txt";
        public string LogFile => Path.Combine(LogDirectory, _logFile);
    }

}

