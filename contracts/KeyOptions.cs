namespace AMSMigrate
{
    public record KeyOptions(
        string AccountName,
        string? ResourceFilter,
        Uri KeyVaultUri,
        string? KeyTemplate,
        int BatchSize);
}
