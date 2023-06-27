
namespace AMSMigrate.Contracts
{
    public interface IFileUploader
    {
        Uri GetDestinationUri(string container, string fileName);

        Task UploadAsync(
            string container,
            string fileName,
            Stream content,
            IProgress<long> progress,
            CancellationToken cancellationToken);

        Task UpdateOutputStatus(
            string containerName,
            CancellationToken cancellationToken);
    }
}
