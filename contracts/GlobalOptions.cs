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
        static private readonly string dateTimeNow = $"{DateTime.Now:HH_mm_ss}";
        private readonly string _logFile = $"MigrationLog_{dateTimeNow}.txt";
        private readonly string _reportFile = $"Report_{dateTimeNow}.html";

        public string LogFile => Path.Combine(LogDirectory, _logFile);
        public string ReportFile => Path.Combine(LogDirectory, _reportFile);

        // Disable interactivity on Linux for the hang issue.
        public bool Interactive { get; set; } = !OperatingSystem.IsLinux();
    }

}

