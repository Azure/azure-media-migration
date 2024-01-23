using Azure.Storage.Blobs;
using System;
using static PlaybackService.PlaybackController;

namespace PlaybackService;

public class AzureServiceFactory : IBlobClientFactory
{
    private readonly DefaultStorageCredential _defaultCredential;

    public AzureServiceFactory(DefaultStorageCredential credential)
    {
        _defaultCredential = credential;
    }

    public BlobServiceClient CreateBlobServiceClient(string blobUri)
    {
        if (blobUri == "UseDevelopmentStorage=true")
        {
            return new BlobServiceClient(blobUri);
        }
        else
        {
            return new BlobServiceClient(new Uri(blobUri), _defaultCredential);
        }
    }
}
