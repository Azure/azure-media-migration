namespace AMSMigrate
{
    public record KeyVaultOptions(Uri KeyVaultUri);

    public record KeyOptions(
        string AccountName,
        string? ResourceFilter,
        Uri KeyVaultUri,
        string? KeyTemplate,
        int BatchSize) : KeyVaultOptions(KeyVaultUri);
}
