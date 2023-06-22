namespace AMSMigrate.Contracts
{
    public enum AnalysisType
    {
        Summary,
        Detailed,
        Report
    }

    public record AnalysisOptions(AnalysisType AnalysisType, int BatchSize);
}
