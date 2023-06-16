
namespace AMSMigrate.Contracts
{
    public interface IFileUploader
    {
        Task UploadAsync(
            string container,
            string fileName,
            Stream content,
            IProgress<long> progress,
            CancellationToken cancellationToken);
    }
}
