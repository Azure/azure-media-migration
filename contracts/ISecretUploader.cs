namespace AMSMigrate.Contracts
{
    internal interface ISecretUploader
    {
        Task UploadAsync(
            string secretName,
            string secretValue,
            CancellationToken cancellationToken);
    }
}
