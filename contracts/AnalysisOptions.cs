namespace AMSMigrate.Contracts
{
    public enum AnalysisType
    {
        Summary,
        Detailed,
        Report
    }

    public record AnalysisOptions(string AccountName, DateTimeOffset? CreationTimeStart, DateTimeOffset? CreationTimeEnd, string? ResourceFilter, AnalysisType AnalysisType, int BatchSize, bool IsStorageAcc =false, string? Prefix = null);
}
