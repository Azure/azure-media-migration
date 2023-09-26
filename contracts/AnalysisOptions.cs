namespace AMSMigrate.Contracts
{
    public record AnalysisOptions(string AccountName, DateTimeOffset? CreationTimeStart, DateTimeOffset? CreationTimeEnd, string? ResourceFilter, int BatchSize);
}
