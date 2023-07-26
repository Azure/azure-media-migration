
namespace AMSMigrate.Contracts
{
    public record Headers(string? ContentType);

    public interface IFileUploader
    {
        Uri GetDestinationUri(string container, string fileName);

        Task UploadAsync(
            string container,
            string fileName,
            Stream content,
            Headers headers,
            IProgress<long> progress,
            CancellationToken cancellationToken);

        Task UpdateOutputStatus(
            string containerName,
            CancellationToken cancellationToken);

        Task<bool> CanUploadAsync(
            string containerName, 
            string outputPath,
            CancellationToken cancellationToken);

        Task UploadCleanupAsync(
            string containerName,
            string outputPath,
            CancellationToken cancellationToken);
    }
}
