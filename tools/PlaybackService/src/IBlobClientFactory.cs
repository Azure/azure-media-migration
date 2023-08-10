using Azure.Storage.Blobs;

namespace PlaybackService;

public interface IBlobClientFactory
{
    BlobServiceClient CreateBlobServiceClient(string blobUri);
}
