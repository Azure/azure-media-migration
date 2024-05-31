using Microsoft.Extensions.Logging;

namespace AMSMigrate.Contracts
{
    public record GlobalOptions(
        string TenantId,
        string SubscriptionId,
        string ResourceGroup,
        CloudType CloudType,
        LogLevel LogLevel,
        string LogDirectory)
    {
        static private readonly string dateTimeNow = $"{DateTime.Now:HH_mm_ss}";
        private readonly string _logFile = $"MigrationLog_{dateTimeNow}.txt";
        private readonly string _htmlReportFile = $"Report_{dateTimeNow}.html";
        private readonly string _jsonReportFile = $"Report_{dateTimeNow}.json";

        public string LogFile => Path.Combine(LogDirectory, _logFile);
        public string HtmlReportFile => Path.Combine(LogDirectory, _htmlReportFile);

        public string JsonReportFile => Path.Combine(LogDirectory, _jsonReportFile);

        // Disable interactivity on Linux for the hang issue.
        public bool Interactive { get; set; } = !OperatingSystem.IsLinux();
    }

}

