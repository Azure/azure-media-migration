namespace AMSMigrate.Contracts
{
    public enum AnalysisType
    {
        Summary,
        Detailed,
        Report
    }

    public record AnalysisOptions(string AccountName, string? ResourceFilter, AnalysisType AnalysisType, int BatchSize);
}
