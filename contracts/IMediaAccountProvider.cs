using Azure.ResourceManager.Media;

namespace AMSMigrate.contracts
{
    internal interface IMediaAccountProvider
    {
        Task<MediaServicesAccountResource> GetMediaAccountAsync(CancellationToken cancellationToken);
    }
}
