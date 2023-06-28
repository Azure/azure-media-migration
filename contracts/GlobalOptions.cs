using Microsoft.Extensions.Logging;

namespace AMSMigrate.Contracts
{
    public record GlobalOptions(
        string SubscriptionId,
        string ResourceGroup,
        CloudType CloudType,       
        LogLevel LogLevel,
        string LogDirectory)
    {
        private readonly string _logFile = $"MigrationLog_{DateTime.Now:HH_mm_ss}.txt";
        public string LogFile => Path.Combine(LogDirectory, _logFile);
    }

}

