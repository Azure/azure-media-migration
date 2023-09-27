using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PlaybackService.Test;

public class PlaybackControllerTest : TestBase
{
    [Fact]
    public async Task DashManifestShouldBeInjected()
    {
        // Create BlobClientFactoryMock.
        var blobClientFactoryMock = CreateBlobClientFactoryMock(
            blobProperties: BlobsModelFactory.BlobProperties(contentType: "abcdefg", contentLength: 12345),
            storageAccountUri: "https://test-storage.blob.core.windows.net/",
            containerName: "test-container",
            blobName: "test-path.mpd",
            blobContent: File.OpenRead("test-data/dash.mpd"));

        // Create HttpContext Mock.
        var (httpContextMock, httpRequestMock, httpResponseMock) = CreateHttpContentMock();

        // Hook HttpResponseMock.
        using var output = new MemoryStream();
        string? contentType = null;
        httpResponseMock.SetupSet(c => c.ContentType = It.IsAny<string?>()).Callback<string?>(c => contentType = c);
        httpResponseMock.Setup(c => c.Body).Returns(output);

        // Create controller
        var controller = new PlaybackController(CreateIOptionSnapshotMock<PlaybackController.Options>(new() { AllowedFileExtensions = new[] { ".mpd" }, SkipCheckStorageHostExists = true }), NullLogger<PlaybackController>.Instance);
        controller.ControllerContext.HttpContext = httpContextMock.Object;

        // Invoke GetAsync
        await controller.GetAsync("test-storage", "test-container", "test-path.mpd", blobClientFactoryMock.Object);

        // Make sure content type is right;
        Assert.Equal("abcdefg", contentType);

        // Make suer output is right.
        AssertOutputIsExpected(output, "test-data/dash.ingested.mpd");

        // Make sure no-cache is set in the response header.
        var cacheControl = httpResponseMock.Object.Headers.CacheControl.ToString();
        Assert.Equal("no-cache, no-store, must-revalidate", cacheControl);

        var expire = httpResponseMock.Object.Headers.Expires.ToString();
        Assert.Equal("0", expire);
    }

    [Fact]
    public async Task HlsManifestShouldBeInjected()
    {
        // Create BlobClientFactoryMock.
        var blobClientFactoryMock = CreateBlobClientFactoryMock(
            blobProperties: BlobsModelFactory.BlobProperties(contentType: "abcdefg", contentLength: 12345),
            storageAccountUri: "https://test-storage.blob.core.windows.net/",
            containerName: "test-container",
            blobName: "test-path.m3u8",
            blobContent: File.OpenRead("test-data/hls.m3u8"));

        // Create HttpContext Mock.
        var (httpContextMock, httpRequestMock, httpResponseMock) = CreateHttpContentMock();

        // Hook HttpResponseMock.
        using var output = new MemoryStream();
        string? contentType = null;
        httpResponseMock.SetupSet(c => c.ContentType = It.IsAny<string?>()).Callback<string?>(c => contentType = c);
        httpResponseMock.Setup(c => c.Body).Returns(output);

        // Create controller
        var controller = new PlaybackController(CreateIOptionSnapshotMock<PlaybackController.Options>(new() { AllowedFileExtensions = new[] { ".m3u8" }, SkipCheckStorageHostExists = true }), NullLogger<PlaybackController>.Instance);
        controller.ControllerContext.HttpContext = httpContextMock.Object;

        // Invoke GetAsync
        await controller.GetAsync("test-storage", "test-container", "test-path.m3u8", blobClientFactoryMock.Object);

        // Make sure content type is right;
        Assert.Equal("abcdefg", contentType);

        // Make suer output is right.
        AssertOutputIsExpected(output, "test-data/hls.ingested.m3u8");

        // Make sure no-cache is set in the response header.
        var cacheControl = httpResponseMock.Object.Headers.CacheControl.ToString();
        Assert.Equal("no-cache, no-store, must-revalidate", cacheControl);

        var expire = httpResponseMock.Object.Headers.Expires.ToString();
        Assert.Equal("0", expire);
    }
}
