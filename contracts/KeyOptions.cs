namespace AMSMigrate
{
    public record KeyOptions(
        Uri KeyVaultUri,
        string? KeyTemplate,
        int BatchSize);
}
